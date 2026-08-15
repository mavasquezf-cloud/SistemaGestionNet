using SistemaGestion.Application.Catalog.Persistence;

namespace SistemaGestion.Application.Catalog.Products.GetProducts;

public sealed class GetProductsUseCase(IProductRepository productRepository)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<PagedProductsResult> ExecuteAsync(
        GetProductsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new GetProductsQuery();

        var page = query.Page < 1 ? DefaultPage : query.Page;
        var pageSize = query.PageSize < 1
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaximumPageSize);

        var productPage = await productRepository.GetPageAsync(page, pageSize, cancellationToken);

        return new PagedProductsResult(
            productPage.Items.Select(item => item.ToResult()).ToArray(),
            page,
            pageSize,
            productPage.TotalCount);
    }
}
