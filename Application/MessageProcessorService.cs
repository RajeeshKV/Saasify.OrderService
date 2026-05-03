using Application;
using Domain;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application
{
    public class MessageProcessorService : BackgroundService
    {
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IOrderService _orderService;
        private readonly ILogger<MessageProcessorService> _logger;
        private readonly string _queueName = "order.queue";

        public MessageProcessorService(
            IRabbitMQService rabbitMQService,
            IOrderService orderService,
            ILogger<MessageProcessorService> logger)
        {
            _rabbitMQService = rabbitMQService;
            _orderService = orderService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Message Processor Service starting...");

            try
            {
                await _rabbitMQService.SubscribeAsync<OrderMessage>(_queueName, async (message) =>
                {
                    await ProcessOrderMessageAsync(message, stoppingToken);
                });

                _logger.LogInformation("Message Processor Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Message Processor Service");
                throw;
            }

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessOrderMessageAsync(OrderMessage message, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Processing order message: CorrelationId={CorrelationId}, TenantId={TenantId}, Amount={Amount}", 
                    message.CorrelationId, message.TenantId, message.Amount);

                // Create order if correlation ID is not set (new order)
                if (string.IsNullOrEmpty(message.CorrelationId))
                {
                    var orderResponse = await _orderService.CreateOrderAsync(message);
                    message.CorrelationId = orderResponse.ExternalOrderId;
                }

                // Process the order
                var success = await _orderService.ProcessOrderAsync(message);

                if (success)
                {
                    _logger.LogInformation("Order processed successfully: CorrelationId={CorrelationId}", message.CorrelationId);
                    
                    // Publish success message (optional)
                    await PublishOrderStatusMessageAsync(message, "Completed");
                }
                else
                {
                    _logger.LogWarning("Order processing failed: CorrelationId={CorrelationId}", message.CorrelationId);
                    
                    // Publish failure message (optional)
                    await PublishOrderStatusMessageAsync(message, "Failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order message: CorrelationId={CorrelationId}", message.CorrelationId);
                
                // Publish error message (optional)
                await PublishOrderStatusMessageAsync(message, "Error");
            }
        }

        private async Task PublishOrderStatusMessageAsync(OrderMessage originalMessage, string status)
        {
            try
            {
                var statusMessage = new OrderStatusMessage
                {
                    CorrelationId = originalMessage.CorrelationId,
                    TenantId = originalMessage.TenantId,
                    UserId = originalMessage.UserId,
                    Amount = originalMessage.Amount,
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    OriginalMessage = originalMessage
                };

                await _rabbitMQService.PublishMessageAsync(statusMessage, "order.status.queue");
                
                _logger.LogInformation("Order status message published: CorrelationId={CorrelationId}, Status={Status}", 
                    statusMessage.CorrelationId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish order status message: CorrelationId={CorrelationId}", 
                    originalMessage.CorrelationId);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Message Processor Service stopping...");
            
            _rabbitMQService.Dispose();
            
            await base.StopAsync(cancellationToken);
        }
    }

    public class OrderStatusMessage
    {
        public string CorrelationId { get; set; }
        public int TenantId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
        public OrderMessage OriginalMessage { get; set; }
    }
}
