using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/health")]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(HealthCheckService healthCheckService, ILogger<HealthController> logger)
        {
            _healthCheckService = healthCheckService;
            _logger = logger;
        }

        [HttpGet, HttpHead]
        public async Task<IActionResult> HealthCheck()
        {
            var healthCheckReport = await _healthCheckService.CheckHealthAsync();
            
            var databaseStatus = healthCheckReport.Entries.ContainsKey("database") 
                ? healthCheckReport.Entries["database"].Status.ToString()
                : "Unknown";
            var rabbitmqStatus = healthCheckReport.Entries.ContainsKey("rabbitmq") 
                ? healthCheckReport.Entries["rabbitmq"].Status.ToString()
                : "Unknown";
            
            var healthStatus = healthCheckReport.Status == HealthStatus.Healthy ? 200 : 503;
            
            // For HEAD requests, return status code only
            if (HttpContext.Request.Method == "HEAD")
            {
                HttpContext.Response.StatusCode = healthStatus;
                return new EmptyResult();
            }
            
            // For GET requests, return full health status
            return new JsonResult(new
            {
                Status = healthCheckReport.Status.ToString(),
                Service = "OrderService",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Database = databaseStatus,
                RabbitMQ = rabbitmqStatus
            });
        }

        [HttpGet("detailed")]
        public async Task<IActionResult> DetailedHealthCheck()
        {
            return new OkObjectResult(await _healthCheckService.CheckHealthAsync());
        }
    }
}
