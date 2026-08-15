namespace SistemaGestion.Application.Catalog.Products.CreateProduct;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    Guid CategoryId,
    string UnitOfMeasure,
    decimal DefaultSalePrice,
    string? Description = null);
