using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Application.Catalog.Products;

public sealed record ProductResult(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    Guid CategoryId,
    string UnitOfMeasure,
    decimal DefaultSalePrice,
    ProductStatus Status);
