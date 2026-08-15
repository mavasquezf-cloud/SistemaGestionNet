namespace SistemaGestion.Application.Catalog.Persistence;

public sealed record ProductPage(
    IReadOnlyList<ProductWithCategory> Items,
    int TotalCount);
