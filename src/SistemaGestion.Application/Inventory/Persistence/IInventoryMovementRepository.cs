using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Application.Inventory.Persistence;

public interface IInventoryMovementRepository
{
    Task AddAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken = default);

    Task<InventoryMovementPage> GetPageByProductIdAsync(
        Guid productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
