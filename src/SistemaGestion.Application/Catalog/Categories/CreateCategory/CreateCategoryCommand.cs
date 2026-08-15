namespace SistemaGestion.Application.Catalog.Categories.CreateCategory;

public sealed record CreateCategoryCommand(string Name, string? Description = null);
