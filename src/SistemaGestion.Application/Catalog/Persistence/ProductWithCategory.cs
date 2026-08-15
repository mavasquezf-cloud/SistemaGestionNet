using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Application.Catalog.Persistence;

public sealed record ProductWithCategory(Product Product, string CategoryName);
