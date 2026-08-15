namespace SistemaGestion.Application.Catalog.Products.CreateProduct;

public sealed record CreateProductResult(
    CreateProductOutcome Outcome,
    ProductResult? Product);
