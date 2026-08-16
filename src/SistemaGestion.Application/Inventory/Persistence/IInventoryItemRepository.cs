using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Application.Inventory.Persistence;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        InventoryItem inventoryItem,
        CancellationToken cancellationToken = default);
}
