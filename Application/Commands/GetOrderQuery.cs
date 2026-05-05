namespace Application.Commands
{
    public record GetOrderQuery(
        int OrderId,
        int TenantId
    );
}
