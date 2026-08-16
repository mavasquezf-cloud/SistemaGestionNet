using Microsoft.EntityFrameworkCore;
using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(SistemaGestionDbContext dbContext) : IProductRepository
{
    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await dbContext.Products.AddAsync(product, cancellationToken);
    }

    public Task<ProductWithCategory?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return (from product in dbContext.Products.AsNoTracking()
                join category in dbContext.Categories.AsNoTracking()
                    on product.CategoryId equals category.Id
                where product.Id == productId
                select new ProductWithCategory(product, category.Name))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var totalCount = await dbContext.Products
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var items = await (from product in dbContext.Products.AsNoTracking()
                           join category in dbContext.Categories.AsNoTracking()
                               on product.CategoryId equals category.Id
                           orderby product.Name, product.Id
                           select new ProductWithCategory(product, category.Name))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ProductPage(items, totalCount);
    }

    public Task<bool> ExistsBySkuAsync(Sku sku, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sku);
        var normalizedSku = new Sku(sku.Value);

        return dbContext.Products
            .AsNoTracking()
            .AnyAsync(product => product.Sku == normalizedSku, cancellationToken);
    }
}
