using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Application.Catalog.Products;

public sealed record ProductWithCategoryResult(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    string UnitOfMeasure,
    decimal DefaultSalePrice,
    ProductStatus Status);
