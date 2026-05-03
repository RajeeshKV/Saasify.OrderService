using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Infrastructure;

namespace Infrastructure.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(OrderDbContext context, ILogger<DatabaseHealthCheck> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check database connection
                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
                
                if (!canConnect)
                {
                    return HealthCheckResult.Unhealthy("Database connection failed");
                }

                // Check if orders table is accessible
                var ordersCount = await _context.Orders.CountAsync(cancellationToken);
                
                _logger.LogInformation("Database health check passed: {OrdersCount} orders found", ordersCount);
                
                return HealthCheckResult.Healthy(
                    "Database is healthy",
                    new Dictionary<string, object>
                    {
                        ["orders_count"] = ordersCount,
                        ["connection"] = "success"
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return HealthCheckResult.Unhealthy(
                    "Database health check failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    });
            }
        }
    }
}
