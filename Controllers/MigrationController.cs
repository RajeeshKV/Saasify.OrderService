using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure;
using System.Diagnostics;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MigrationController : ControllerBase
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<MigrationController> _logger;

        public MigrationController(OrderDbContext context, ILogger<MigrationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetMigrationStatus()
        {
            try
            {
                var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                var allMigrations = await _context.Database.GetMigrationsAsync();

                var canConnect = await _context.Database.CanConnectAsync();

                var status = new
                {
                    CanConnect = canConnect,
                    AppliedMigrations = appliedMigrations.ToList(),
                    PendingMigrations = pendingMigrations.ToList(),
                    AllMigrations = allMigrations.ToList(),
                    TotalApplied = appliedMigrations.Count(),
                    TotalPending = pendingMigrations.Count(),
                    IsUpToDate = !pendingMigrations.Any(),
                    DatabaseInfo = new
                    {
                        ConnectionString = MaskConnectionString(_context.Database.GetConnectionString()),
                        Provider = _context.Database.ProviderName,
                        CanConnect = canConnect
                    },
                    Timestamp = DateTime.UtcNow
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get migration status");
                return StatusCode(500, new { error = "Failed to get migration status", details = ex.Message });
            }
        }

        [HttpPost("apply")]
        public async Task<IActionResult> ApplyMigrations()
        {
            try
            {
                _logger.LogInformation("Starting database migration application");

                var stopwatch = Stopwatch.StartNew();

                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                
                if (!pendingMigrations.Any())
                {
                    _logger.LogInformation("No pending migrations to apply");
                    return Ok(new { 
                        message = "No pending migrations to apply",
                        appliedCount = 0,
                        durationMs = stopwatch.ElapsedMilliseconds
                    });
                }

                await _context.Database.MigrateAsync();

                stopwatch.Stop();

                var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();

                _logger.LogInformation("Database migrations applied successfully: {Count} migrations in {DurationMs}ms", 
                    pendingMigrations.Count(), stopwatch.ElapsedMilliseconds);

                return Ok(new
                {
                    message = "Migrations applied successfully",
                    appliedCount = pendingMigrations.Count(),
                    appliedMigrations = appliedMigrations.ToList(),
                    durationMs = stopwatch.ElapsedMilliseconds,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply database migrations");
                return StatusCode(500, new { error = "Failed to apply migrations", details = ex.Message });
            }
        }

        [HttpPost("reset")]
        public async Task<IActionResult> ResetDatabase()
        {
            try
            {
                _logger.LogWarning("Database reset requested - this will delete all data");

                // Only allow in development
                if (!Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid("Database reset is only allowed in development environment");
                }

                var stopwatch = Stopwatch.StartNew();

                // Ensure database is created
                await _context.Database.EnsureCreatedAsync();

                // Apply all migrations
                await _context.Database.MigrateAsync();

                stopwatch.Stop();

                _logger.LogInformation("Database reset and migrations applied successfully in {DurationMs}ms", 
                    stopwatch.ElapsedMilliseconds);

                return Ok(new
                {
                    message = "Database reset and migrations applied successfully",
                    durationMs = stopwatch.ElapsedMilliseconds,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset database");
                return StatusCode(500, new { error = "Failed to reset database", details = ex.Message });
            }
        }

        private string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "Not configured";

            // Simple masking - hide password
            var parts = connectionString.Split(';');
            var maskedParts = parts.Select(part =>
            {
                if (part.StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                {
                    return "Password=***MASKED***";
                }
                return part;
            });

            return string.Join(";", maskedParts);
        }
    }
}
