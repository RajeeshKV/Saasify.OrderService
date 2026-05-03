using Microsoft.Extensions.Diagnostics.HealthChecks;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.HealthChecks
{
    public class RabbitMQHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMQHealthCheck> _logger;

        public RabbitMQHealthCheck(IServiceProvider serviceProvider, ILogger<RabbitMQHealthCheck> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get RabbitMQ service from service provider
                using var scope = _serviceProvider.CreateScope();
                var rabbitMQService = scope.ServiceProvider.GetRequiredService<IRabbitMQService>();

                // Check if RabbitMQ service is connected
                if (rabbitMQService is RabbitMQService concreteService)
                {
                    // Wait a moment for connection to be established if it's still initializing
                    if (!concreteService.IsConnected)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                    
                    if (concreteService.IsConnected)
                    {
                        _logger.LogInformation("RabbitMQ health check passed: connection is healthy");
                        
                        return HealthCheckResult.Healthy(
                            "RabbitMQ is healthy",
                            new Dictionary<string, object>
                            {
                                ["connection"] = "success",
                                ["status"] = "connected"
                            });
                    }
                    else
                    {
                        return HealthCheckResult.Unhealthy("RabbitMQ is not connected");
                    }
                }

                return HealthCheckResult.Unhealthy("RabbitMQ service is not available");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ health check failed");
                return HealthCheckResult.Unhealthy(
                    "RabbitMQ health check failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    });
            }
        }
    }
}
