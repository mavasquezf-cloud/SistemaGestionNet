namespace SistemaGestion.Application.Catalog.Products.GetProducts;

public sealed record PagedProductsResult(
    IReadOnlyList<ProductWithCategoryResult> Items,
    int Page,
    int PageSize,
    int TotalCount);
