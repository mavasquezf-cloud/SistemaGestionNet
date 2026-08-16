using Microsoft.EntityFrameworkCore;
using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Domain.Catalog.Categories;

namespace SistemaGestion.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(SistemaGestionDbContext dbContext) : ICategoryRepository
{
    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await dbContext.Categories.AddAsync(category, cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return dbContext.Categories
            .AsNoTracking()
            .AnyAsync(category => category.Id == categoryId, cancellationToken);
    }
}
