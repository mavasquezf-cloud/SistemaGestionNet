using SistemaGestion.Application.Suppliers.Persistence;

namespace SistemaGestion.Application.Suppliers.GetSuppliers;

public sealed class GetSuppliersUseCase(ISupplierRepository supplierRepository)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<PagedSuppliersResult> ExecuteAsync(
        GetSuppliersQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new GetSuppliersQuery();
        var page = query.Page < 1 ? DefaultPage : query.Page;
        var pageSize = query.PageSize < 1
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaximumPageSize);

        var supplierPage = await supplierRepository.GetPageAsync(
            page, pageSize, cancellationToken);

        return new PagedSuppliersResult(
            supplierPage.Items.Select(SupplierResult.FromSupplier).ToArray(),
            page,
            pageSize,
            supplierPage.TotalCount);
    }
}
