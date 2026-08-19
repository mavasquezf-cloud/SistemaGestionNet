using SistemaGestion.Application.Purchasing.Persistence;

namespace SistemaGestion.Application.Purchasing.GetPurchases;

public sealed record GetPurchasesQuery(int Page = 1, int PageSize = 20);
public sealed record PagedPurchasesResult(IReadOnlyCollection<PurchaseResult> Items, int Page, int PageSize, int TotalCount);
public sealed class GetPurchasesUseCase(IPurchaseRepository purchases)
{
    public async Task<PagedPurchasesResult> ExecuteAsync(GetPurchasesQuery? query = null, CancellationToken cancellationToken = default)
    {
        query ??= new();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, 100);
        var result = await purchases.GetPageAsync(page, pageSize, cancellationToken);
        return new(result.Items.Select(PurchaseResult.FromPurchase).ToArray(), page, pageSize, result.TotalCount);
    }
}
