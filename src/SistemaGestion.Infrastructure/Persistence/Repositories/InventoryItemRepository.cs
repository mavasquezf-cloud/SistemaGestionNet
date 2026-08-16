using Microsoft.EntityFrameworkCore;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Infrastructure.Persistence.Repositories;

internal sealed class InventoryItemRepository(SistemaGestionDbContext dbContext)
    : IInventoryItemRepository
{
    public Task<InventoryItem?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.InventoryItems.SingleOrDefaultAsync(
            item => item.ProductId == productId, cancellationToken);
    }

    public async Task AddAsync(
        InventoryItem inventoryItem,
        CancellationToken cancellationToken = default)
    {
        await dbContext.InventoryItems.AddAsync(inventoryItem, cancellationToken);
    }
}
