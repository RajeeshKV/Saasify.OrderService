namespace Application.Commands
{
    public record UpdateOrderStatusCommand(
        int OrderId,
        int TenantId,
        string Status
    );
}
