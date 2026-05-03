using Application;
using Domain;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Npgsql;

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
            
            // Keep the subscription alive
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _rabbitMQService.SubscribeAsync<OrderMessage>(
                        _queueName, 
                        ProcessOrderMessageAsync);
                    
                    // If we reach here, the subscription was closed, wait and retry
                    await Task.Delay(5000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in message subscription, retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task ProcessOrderMessageAsync(OrderMessage message)
        {
            int? actualOrderId = null;
            
            try
            {
                _logger.LogInformation("Processing order message: CorrelationId={CorrelationId}, TenantId={TenantId}, Amount={Amount}", 
                    message.CorrelationId, message.TenantId, message.Amount);
                
                using var scope = _serviceProvider.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                
                // Create order
                var orderResponse = await orderService.CreateOrderAsync(message);
                actualOrderId = orderResponse.OrderId;
                
                _logger.LogInformation("Created new order: OrderId={OrderId}, ExternalOrderId={ExternalOrderId}", 
                    orderResponse.OrderId, orderResponse.ExternalOrderId);
                
                // Simulate payment processing
                var result = await orderService.ProcessOrderAsync(message);
                
                // Publish status update with actual order ID
                await PublishOrderStatusAsync(message, result, actualOrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order message: CorrelationId={CorrelationId}", message.CorrelationId);
                
                // Publish error message with actual order ID if available
                await PublishOrderStatusAsync(message, false, actualOrderId);
            }
        }

        private async Task PublishOrderStatusAsync(OrderMessage message, bool result, int? actualOrderId = null)
        {
            var statusMessage = new Domain.OrderStatusMessage
            {
                OrderId = actualOrderId ?? 0, // Use actual order ID if available
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
                    
                _logger.LogInformation("Published order status: CorrelationId={CorrelationId}, OrderId={OrderId}, Status={Status}", 
                    message.CorrelationId, actualOrderId, result ? "Completed" : "Failed");
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
}
