using SistemaGestion.Domain.Catalog.Categories;

namespace SistemaGestion.Application.Catalog.Persistence;

public interface ICategoryRepository
{
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid categoryId, CancellationToken cancellationToken = default);
}
