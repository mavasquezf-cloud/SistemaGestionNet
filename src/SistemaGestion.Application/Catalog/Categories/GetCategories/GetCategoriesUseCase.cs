using SistemaGestion.Application.Catalog.Persistence;

namespace SistemaGestion.Application.Catalog.Categories.GetCategories;

public sealed class GetCategoriesUseCase(ICategoryRepository categoryRepository)
{
    public async Task<IReadOnlyList<CategoryResult>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await categoryRepository.GetAllAsync(cancellationToken);

        return categories
            .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(category => new CategoryResult(
                category.Id,
                category.Name,
                category.Description,
                category.IsActive))
            .ToArray();
    }
}
