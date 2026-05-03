using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Infrastructure;

namespace Infrastructure.HealthChecks
{
    public class MigrationHealthCheck : IHealthCheck
    {
        private readonly OrderDbContext _context;

        public MigrationHealthCheck(OrderDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if database is reachable
                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
                if (!canConnect)
                {
                    return HealthCheckResult.Unhealthy(
                        "Database connection failed",
                        new Dictionary<string, object>
                        {
                            ["status"] = "disconnected"
                        });
                }

                // Check if all migrations are applied
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
                if (pendingMigrations.Any())
                {
                    return HealthCheckResult.Degraded(
                        "Pending migrations exist",
                        new Dictionary<string, object>
                        {
                            ["status"] = "pending_migrations",
                            ["pending_count"] = pendingMigrations.Count(),
                            ["migrations"] = pendingMigrations.ToList()
                        });
                }

                // Check if orders table exists and is accessible
                var ordersCount = await _context.Orders.CountAsync(cancellationToken);
                
                return HealthCheckResult.Healthy(
                    "Database is healthy and all migrations applied",
                    new Dictionary<string, object>
                    {
                        ["status"] = "healthy",
                        ["orders_count"] = ordersCount,
                        ["connection"] = "success"
                    });
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    "Database health check failed",
                    new Dictionary<string, object>
                    {
                        ["status"] = "error",
                        ["error"] = ex.Message
                    });
            }
        }
    }
}
