using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Application.Inventory.Persistence;

public interface IInventoryItemRepository
{
    async Task<IReadOnlyDictionary<Guid, InventoryItem>> GetByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        var items = new Dictionary<Guid, InventoryItem>();
        foreach (var productId in productIds.Distinct())
        {
            var item = await GetByProductIdAsync(productId, cancellationToken);
            if (item is not null) items[productId] = item;
        }
        return items;
    }

    Task<InventoryItem?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        InventoryItem inventoryItem,
        CancellationToken cancellationToken = default);
}
