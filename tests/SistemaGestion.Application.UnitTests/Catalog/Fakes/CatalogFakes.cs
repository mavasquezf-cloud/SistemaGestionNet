using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Domain.Catalog.Categories;
using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Application.UnitTests.Catalog.Fakes;

internal sealed class FakeCategoryRepository : ICategoryRepository
{
    public List<Category> Categories { get; } = [];

    public Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        Categories.Add(category);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Category>>(Categories.ToArray());
    }

    public Task<bool> ExistsAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Categories.Any(category => category.Id == categoryId));
    }
}

internal sealed class FakeProductRepository(FakeCategoryRepository categoryRepository) : IProductRepository
{
    public List<Product> Products { get; } = [];

    public int? RequestedPage { get; private set; }

    public int? RequestedPageSize { get; private set; }

    public Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        Products.Add(product);
        return Task.CompletedTask;
    }

    public Task<ProductWithCategory?> GetByIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var product = Products.SingleOrDefault(item => item.Id == productId);
        return Task.FromResult(product is null ? null : WithCategory(product));
    }

    public Task<ProductPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        RequestedPage = page;
        RequestedPageSize = pageSize;

        var items = Products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(WithCategory)
            .ToArray();

        return Task.FromResult(new ProductPage(items, Products.Count));
    }

    public Task<bool> ExistsBySkuAsync(Sku sku, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Products.Any(product => product.Sku == sku));
    }

    private ProductWithCategory WithCategory(Product product)
    {
        var categoryName = categoryRepository.Categories
            .Single(category => category.Id == product.CategoryId)
            .Name;

        return new ProductWithCategory(product, categoryName);
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }
}
