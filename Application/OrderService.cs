using Domain;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics;

namespace Application
{
    public interface IOrderService
    {
        Task<OrderResponse> CreateOrderAsync(OrderMessage message);
        Task<Order> GetOrderAsync(int orderId, int tenantId);
        Task<IEnumerable<Order>> GetOrdersByTenantAsync(int tenantId, int page = 1, int pageSize = 10);
        Task<OrderResponse> UpdateOrderStatusAsync(int orderId, int tenantId, string status);
        Task<bool> ProcessOrderAsync(OrderMessage message);
    }

    public class OrderService : IOrderService
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderService> _logger;
        private readonly ActivitySource _activitySource = new("OrderService");

        public OrderService(OrderDbContext context, ILogger<OrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<OrderResponse> CreateOrderAsync(OrderMessage message)
        {
            using var activity = _activitySource.StartActivity("CreateOrder", ActivityKind.Internal);
            activity?.SetTag("tenant.id", message.TenantId.ToString());
            activity?.SetTag("user.id", message.UserId.ToString());
            activity?.SetTag("order.amount", message.Amount.ToString());

            _logger.LogInformation("Starting order creation: TenantId={TenantId}, UserId={UserId}, Amount={Amount}, CorrelationId={CorrelationId}", 
                message.TenantId, message.UserId, message.Amount, message.CorrelationId);

            try
            {
                var order = new Order
                {
                    TenantId = message.TenantId,
                    UserId = message.UserId,
                    Amount = message.Amount,
                    Currency = message.Currency,
                    Description = message.Description,
                    CustomerEmail = message.CustomerEmail,
                    Metadata = message.Metadata,
                    Status = "Created",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExternalOrderId = GenerateExternalOrderId()
                };

                _context.Orders.Add(order);
                
                var stopwatch = Stopwatch.StartNew();
                await _context.SaveChangesAsync();
                stopwatch.Stop();

                activity?.SetTag("order.id", order.Id.ToString());
                activity?.SetTag("order.external_id", order.ExternalOrderId);
                activity?.SetTag("db.duration_ms", stopwatch.ElapsedMilliseconds.ToString());

                _logger.LogInformation("Order created successfully: OrderId={OrderId}, TenantId={TenantId}, UserId={UserId}, Amount={Amount}, ExternalOrderId={ExternalOrderId}, DbDurationMs={DbDurationMs}", 
                    order.Id, order.TenantId, order.UserId, order.Amount, order.ExternalOrderId, stopwatch.ElapsedMilliseconds);

                return new OrderResponse
                {
                    OrderId = order.Id,
                    TenantId = order.TenantId,
                    UserId = order.UserId,
                    Amount = order.Amount,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    ExternalOrderId = order.ExternalOrderId
                };
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Failed to create order for TenantId={TenantId}, UserId={UserId}, Amount={Amount}, CorrelationId={CorrelationId}", 
                    message.TenantId, message.UserId, message.Amount, message.CorrelationId);
                throw;
            }
        }

        public async Task<Order> GetOrderAsync(int orderId, int tenantId)
        {
            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);

                if (order == null)
                {
                    _logger.LogWarning("Order not found: OrderId={OrderId}, TenantId={TenantId}", orderId, tenantId);
                }

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order: OrderId={OrderId}, TenantId={TenantId}", orderId, tenantId);
                throw;
            }
        }

        public async Task<IEnumerable<Order>> GetOrdersByTenantAsync(int tenantId, int page = 1, int pageSize = 10)
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => o.TenantId == tenantId)
                    .OrderByDescending(o => o.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} orders for TenantId={TenantId}, Page={Page}, PageSize={PageSize}", 
                    orders.Count, tenantId, page, pageSize);

                return orders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get orders for TenantId={TenantId}", tenantId);
                throw;
            }
        }

        public async Task<OrderResponse> UpdateOrderStatusAsync(int orderId, int tenantId, string status)
        {
            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);

                if (order == null)
                {
                    _logger.LogWarning("Order not found for status update: OrderId={OrderId}, TenantId={TenantId}", orderId, tenantId);
                    return null;
                }

                order.Status = status;
                order.UpdatedAt = DateTime.UtcNow;

                if (status == "Completed" || status == "Failed")
                {
                    order.ProcessedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Order status updated: OrderId={OrderId}, TenantId={TenantId}, Status={Status}", 
                    orderId, tenantId, status);

                return new OrderResponse
                {
                    OrderId = order.Id,
                    TenantId = order.TenantId,
                    UserId = order.UserId,
                    Amount = order.Amount,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    ExternalOrderId = order.ExternalOrderId,
                    ErrorMessage = order.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update order status: OrderId={OrderId}, TenantId={TenantId}, Status={Status}", 
                    orderId, tenantId, status);
                throw;
            }
        }

        public async Task<bool> ProcessOrderAsync(OrderMessage message)
        {
            using var activity = _activitySource.StartActivity("ProcessOrder", ActivityKind.Internal);
            activity?.SetTag("tenant.id", message.TenantId.ToString());
            activity?.SetTag("correlation.id", message.CorrelationId);
            activity?.SetTag("order.amount", message.Amount.ToString());
            
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting order processing: TenantId={TenantId}, CorrelationId={CorrelationId}, Amount={Amount}", 
                message.TenantId, message.CorrelationId, message.Amount);

            var processingStopwatch = Stopwatch.StartNew();
            
            try
            {
                // Simulate order processing
                var processingDelay = Random.Shared.Next(500, 2000);
                _logger.LogDebug("Simulating order processing delay: {DelayMs}ms", processingDelay);
                await Task.Delay(processingDelay);

                // Find order by correlation ID
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.ExternalOrderId == message.CorrelationId);

                if (order != null)
                {
                    activity?.SetTag("order.id", order.Id.ToString());
                    _logger.LogInformation("Order found for processing: OrderId={OrderId}, ExternalOrderId={ExternalOrderId}, CurrentStatus={CurrentStatus}", 
                        order.Id, order.ExternalOrderId, order.Status);

                    // Update order status to processing
                    order.Status = "Processing";
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Order status updated to Processing: OrderId={OrderId}, ExternalOrderId={ExternalOrderId}", 
                        order.Id, order.ExternalOrderId);

                    // Simulate external payment processing
                    var paymentStopwatch = Stopwatch.StartNew();
                    var paymentSuccess = await ProcessPaymentAsync(message);
                    paymentStopwatch.Stop();

                    activity?.SetTag("payment.success", paymentSuccess.ToString());
                    activity?.SetTag("payment.duration_ms", paymentStopwatch.ElapsedMilliseconds.ToString());

                    if (paymentSuccess)
                    {
                        order.Status = "Completed";
                        order.ProcessedAt = DateTime.UtcNow;
                        order.ErrorMessage = null;
                        
                        _logger.LogInformation("Order processed successfully: OrderId={OrderId}, ExternalOrderId={ExternalOrderId}, PaymentDurationMs={PaymentDurationMs}, TotalProcessingMs={TotalProcessingMs}", 
                            order.Id, order.ExternalOrderId, paymentStopwatch.ElapsedMilliseconds, processingStopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        order.Status = "Failed";
                        order.ErrorMessage = "Payment processing failed";
                        order.RetryCount++;
                        
                        _logger.LogWarning("Order processing failed: OrderId={OrderId}, ExternalOrderId={ExternalOrderId}, RetryCount={RetryCount}, PaymentDurationMs={PaymentDurationMs}", 
                            order.Id, order.ExternalOrderId, order.RetryCount, paymentStopwatch.ElapsedMilliseconds);
                    }

                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    processingStopwatch.Stop();
                    activity?.SetTag("processing.duration_ms", processingStopwatch.ElapsedMilliseconds.ToString());
                    activity?.SetStatus(paymentSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

                    return paymentSuccess;
                }
                else
                {
                    _logger.LogWarning("Order not found for processing: ExternalOrderId={ExternalOrderId}, TenantId={TenantId}", 
                        message.CorrelationId, message.TenantId);
                    activity?.SetStatus(ActivityStatusCode.Error, "Order not found");
                    return false;
                }
            }
            catch (Exception ex)
            {
                processingStopwatch.Stop();
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Failed to process order: ExternalOrderId={ExternalOrderId}, TenantId={TenantId}, ProcessingMs={ProcessingMs}, Error={Error}", 
                    message.CorrelationId, message.TenantId, processingStopwatch.ElapsedMilliseconds, ex.Message);
                return false;
            }
        }

        private async Task<bool> ProcessPaymentAsync(OrderMessage message)
        {
            // Simulate payment processing with external provider
            // In real implementation, this would call Stripe, PayPal, etc.
            await Task.Delay(500);

            // Simulate 90% success rate
            return Random.Shared.Next(1, 100) <= 90;
        }

        private string GenerateExternalOrderId()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }
    }
}
