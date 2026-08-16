using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Application.UnitTests.Inventory.Fakes;

internal sealed class FakeProductRepository : IProductRepository
{
    public List<Product> Products { get; } = [];

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
        return Task.FromResult(product is null
            ? null
            : new ProductWithCategory(product, "Test category"));
    }

    public Task<ProductPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> ExistsBySkuAsync(Sku sku, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class FakeInventoryItemRepository : IInventoryItemRepository
{
    public List<InventoryItem> Items { get; } = [];

    public int AddCallCount { get; private set; }

    public Task<InventoryItem?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(item => item.ProductId == productId));

    public Task AddAsync(InventoryItem inventoryItem, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        Items.Add(inventoryItem);
        return Task.CompletedTask;
    }
}

internal sealed class FakeInventoryMovementRepository : IInventoryMovementRepository
{
    public List<InventoryMovement> Movements { get; } = [];

    public int? RequestedPage { get; private set; }

    public int? RequestedPageSize { get; private set; }

    public Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
    {
        Movements.Add(movement);
        return Task.CompletedTask;
    }

    public Task<InventoryMovementPage> GetPageByProductIdAsync(
        Guid productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        RequestedPage = page;
        RequestedPageSize = pageSize;
        var matching = Movements
            .Where(movement => movement.ProductId == productId)
            .OrderByDescending(movement => movement.OccurredAt)
            .ToArray();
        var items = matching.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return Task.FromResult(new InventoryMovementPage(items, matching.Length));
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public bool ThrowConcurrencyConflict { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        if (ThrowConcurrencyConflict)
        {
            throw new InventoryConcurrencyException("The inventory item was changed concurrently.");
        }

        return Task.FromResult(1);
    }
}

internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
