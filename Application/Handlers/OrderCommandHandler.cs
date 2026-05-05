using Application.Commands;
using Application;
using Domain;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Handlers
{
    public class OrderCommandHandler : IOrderCommandHandler
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderCommandHandler> _logger;

        public OrderCommandHandler(OrderDbContext context, ILogger<OrderCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<OrderResponse> Handle(CreateOrderCommand command)
        {
            var order = new Order
            {
                TenantId = command.TenantId,
                UserId = command.UserId,
                Amount = command.Amount,
                Currency = command.Currency,
                Description = command.Description,
                CustomerEmail = command.CustomerEmail,
                Metadata = command.Metadata,
                Status = "Created",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExternalOrderId = GenerateExternalOrderId()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

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

        public async Task<Order?> Handle(GetOrderQuery query)
        {
            return await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == query.OrderId && o.TenantId == query.TenantId);
        }

        public async Task<IEnumerable<Order>> Handle(GetOrdersQuery query)
        {
            return await _context.Orders
                .Where(o => o.TenantId == query.TenantId)
                .OrderByDescending(o => o.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();
        }

        public async Task<OrderResponse?> Handle(UpdateOrderStatusCommand command)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == command.OrderId && o.TenantId == command.TenantId);

            if (order == null)
                return null;

            order.Status = command.Status;
            order.UpdatedAt = DateTime.UtcNow;

            if (command.Status == "Completed" || command.Status == "Failed")
            {
                order.ProcessedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

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

        private string GenerateExternalOrderId()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }
    }
}
