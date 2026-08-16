using System.ComponentModel.DataAnnotations;
using SistemaGestion.Application.Catalog.Categories;
using SistemaGestion.Application.Catalog.Products;

namespace SistemaGestion.API.Contracts;

public sealed record CreateCategoryRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string Name,
    [property: StringLength(500)] string? Description = null);

public sealed record CategoryResponse(Guid Id, string Name, string? Description, bool IsActive)
{
    public static CategoryResponse FromResult(CategoryResult result) =>
        new(result.Id, result.Name, result.Description, result.IsActive);
}

public sealed record CreateProductRequest(
    [property: Required, StringLength(64, MinimumLength = 1)] string Sku,
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    Guid CategoryId,
    [property: Required, StringLength(50, MinimumLength = 1)] string UnitOfMeasure,
    [property: Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal DefaultSalePrice,
    [property: StringLength(1000)] string? Description = null);

public sealed record ProductResponse(
    Guid Id, string Sku, string Name, string? Description, Guid CategoryId,
    string UnitOfMeasure, decimal DefaultSalePrice, string Status)
{
    public static ProductResponse FromResult(ProductResult result) => new(
        result.Id, result.Sku, result.Name, result.Description, result.CategoryId,
        result.UnitOfMeasure, result.DefaultSalePrice, result.Status.ToString());
}

public sealed record ProductDetailResponse(
    Guid Id, string Sku, string Name, string? Description, Guid CategoryId,
    string CategoryName, string UnitOfMeasure, decimal DefaultSalePrice, string Status)
{
    public static ProductDetailResponse FromResult(ProductWithCategoryResult result) => new(
        result.Id, result.Sku, result.Name, result.Description, result.CategoryId,
        result.CategoryName, result.UnitOfMeasure, result.DefaultSalePrice, result.Status.ToString());
}

public sealed record PagedProductsResponse(
    IReadOnlyList<ProductDetailResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
