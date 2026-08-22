using SistemaGestion.Application.Customers.Persistence;

namespace SistemaGestion.Application.Customers.GetCustomers;

public sealed record GetCustomersQuery(int Page = 1, int PageSize = 20);
public sealed record PagedCustomersResult(IReadOnlyList<CustomerResult> Items,
    int Page, int PageSize, int TotalCount);

public sealed class GetCustomersUseCase(ICustomerRepository customers)
{
    public async Task<PagedCustomersResult> ExecuteAsync(
        GetCustomersQuery? query = null, CancellationToken cancellationToken = default)
    {
        query ??= new();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, 100);
        var result = await customers.GetPageAsync(page, pageSize, cancellationToken);
        return new(result.Items.Select(CustomerResult.FromCustomer).ToArray(),
            page, pageSize, result.TotalCount);
    }
}
