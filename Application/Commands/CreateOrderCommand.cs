using Domain;

namespace Application.Commands
{
    public record CreateOrderCommand(
        int TenantId,
        int UserId,
        decimal Amount,
        string Currency,
        string Description,
        string CustomerEmail,
        Dictionary<string, string> Metadata
    );
}
