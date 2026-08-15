namespace SistemaGestion.Application.Catalog.Categories;

public sealed record CategoryResult(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive);
