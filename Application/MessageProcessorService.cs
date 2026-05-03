using Application;
using Domain;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Application
{
    public class MessageProcessorService : BackgroundService
    {
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MessageProcessorService> _logger;
        private readonly string _queueName;
        private readonly string _statusQueueName;

        public MessageProcessorService(
            IRabbitMQService rabbitMQService,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<MessageProcessorService> logger)
        {
            _rabbitMQService = rabbitMQService;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _queueName = configuration["OrderQueue:Queue"] ?? "order.queue";
            _statusQueueName = configuration["OrderQueue:StatusQueue"] ?? "order.status.queue";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MessageProcessorService starting...");
            
            await _rabbitMQService.SubscribeAsync<OrderMessage>(
                _queueName, 
                ProcessOrderMessageAsync);
        }

        private async Task ProcessOrderMessageAsync(OrderMessage message)
        {
            try
            {
                _logger.LogInformation("Processing order message: CorrelationId={CorrelationId}, TenantId={TenantId}, Amount={Amount}", 
                    message.CorrelationId, message.TenantId, message.Amount);
                
                using var scope = _serviceProvider.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                
                // Create order
                var orderResponse = await orderService.CreateOrderAsync(message);
                _logger.LogInformation("Created new order: OrderId={OrderId}, ExternalOrderId={ExternalOrderId}", 
                    orderResponse.OrderId, orderResponse.ExternalOrderId);
                
                // Simulate payment processing
                var result = await orderService.ProcessOrderAsync(message);
                
                // Publish status update
                await PublishOrderStatusAsync(message, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order message: CorrelationId={CorrelationId}", message.CorrelationId);
                
                // Publish error message (optional)
                await PublishOrderStatusAsync(message, false);
            }
        }

        private async Task PublishOrderStatusAsync(OrderMessage message, bool result)
        {
            var statusMessage = new Domain.OrderStatusMessage
            {
                OrderId = int.TryParse(message.CorrelationId, out var orderId) ? orderId : 0, // Use correlation as order ID
                TenantId = message.TenantId,
                Status = result ? "Completed" : "Failed",
                CorrelationId = message.CorrelationId,
                OriginalMessage = message
            };

            try
            {
                await _rabbitMQService.PublishMessageAsync(
                    statusMessage, 
                    _statusQueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish order status message: CorrelationId={CorrelationId}", 
                    message.CorrelationId);
            }
        }

        private async Task PublishOrderStatusMessageAsync(OrderMessage message, string status)
        {
            var statusMessage = new Domain.OrderStatusMessage
            {
                OrderId = int.TryParse(message.CorrelationId, out var orderId) ? orderId : 0, // Use correlation as order ID
                TenantId = message.TenantId,
                Status = status,
                CorrelationId = message.CorrelationId,
                OriginalMessage = message
            };

            try
            {
                await _rabbitMQService.PublishMessageAsync(
                    statusMessage, 
                    _statusQueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish order status message: CorrelationId={CorrelationId}", 
                    message.CorrelationId);
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
