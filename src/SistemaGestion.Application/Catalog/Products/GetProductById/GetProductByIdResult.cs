namespace SistemaGestion.Application.Catalog.Products.GetProductById;

public sealed record GetProductByIdResult(
    bool Found,
    ProductWithCategoryResult? Product);
