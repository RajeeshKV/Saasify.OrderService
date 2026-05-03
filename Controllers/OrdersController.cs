using Application;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                // Get tenant ID from JWT claims
                var tenantIdClaim = User.FindFirst("TenantId")?.Value;
                var userIdClaim = User.FindFirst("UserId")?.Value;

                if (!int.TryParse(tenantIdClaim, out var tenantId) || !int.TryParse(userIdClaim, out var userId))
                {
                    return BadRequest("Invalid tenant or user ID in token");
                }

                var orderMessage = new OrderMessage
                {
                    TenantId = tenantId,
                    UserId = userId,
                    Amount = request.Amount,
                    Currency = request.Currency ?? "USD",
                    Description = request.Description,
                    CustomerEmail = request.CustomerEmail,
                    Metadata = request.Metadata ?? new Dictionary<string, string>(),
                    CorrelationId = Guid.NewGuid().ToString()
                };

                var order = await _orderService.CreateOrderAsync(orderMessage);
                
                _logger.LogInformation("Order created via API: OrderId={OrderId}, TenantId={TenantId}", 
                    order.OrderId, order.TenantId);

                return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create order via API");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(int id)
        {
            try
            {
                var tenantIdClaim = User.FindFirst("TenantId")?.Value;
                if (!int.TryParse(tenantIdClaim, out var tenantId))
                {
                    return BadRequest("Invalid tenant ID in token");
                }

                var order = await _orderService.GetOrderAsync(id, tenantId);
                
                if (order is null)
                {
                    return NotFound();
                }

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order: OrderId={OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10)
        {
            var tenantIdClaim = User.FindFirst("TenantId")?.Value;
            
            try
            {
                if (!int.TryParse(tenantIdClaim, out var tenantId))
                {
                    return BadRequest("Invalid tenant ID in token");
                }

                var orders = await _orderService.GetOrdersByTenantAsync(tenantId, page, pageSize);
                
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get orders for TenantId={TenantId}", tenantIdClaim);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/status")]
        public async Task<ActionResult<OrderResponse>> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            try
            {
                var tenantIdClaim = User.FindFirst("TenantId")?.Value;
                if (!int.TryParse(tenantIdClaim, out var tenantId))
                {
                    return BadRequest("Invalid tenant ID in token");
                }

                var order = await _orderService.UpdateOrderStatusAsync(id, tenantId, request.Status);
                
                if (order == null)
                {
                    return NotFound();
                }

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update order status: OrderId={OrderId}, Status={Status}", id, request.Status);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Service = "OrderService",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0"
            });
        }
    }

    public record CreateOrderRequest(
        decimal Amount,
        string? Currency,
        string Description,
        string? CustomerEmail,
        Dictionary<string, string>? Metadata
    );

    public record UpdateOrderStatusRequest(
        string Status
    );
}
