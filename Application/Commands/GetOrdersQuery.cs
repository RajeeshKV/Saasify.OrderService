namespace Application.Commands
{
    public record GetOrdersQuery(
        int TenantId,
        int Page = 1,
        int PageSize = 10
    );
}
