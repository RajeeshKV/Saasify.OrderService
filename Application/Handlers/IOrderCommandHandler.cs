using Application.Commands;
using Application;
using Domain;

namespace Application.Handlers
{
    public interface IOrderCommandHandler
    {
        Task<OrderResponse> Handle(CreateOrderCommand command);
        Task<Order?> Handle(GetOrderQuery query);
        Task<IEnumerable<Order>> Handle(GetOrdersQuery query);
        Task<OrderResponse?> Handle(UpdateOrderStatusCommand command);
    }
}
