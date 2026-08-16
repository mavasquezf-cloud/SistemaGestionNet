using Microsoft.EntityFrameworkCore;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Infrastructure.Persistence.Repositories;

internal sealed class InventoryMovementRepository(SistemaGestionDbContext dbContext)
    : IInventoryMovementRepository
{
    public async Task AddAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken = default)
    {
        await dbContext.InventoryMovements.AddAsync(movement, cancellationToken);
    }

    public async Task<InventoryMovementPage> GetPageByProductIdAsync(
        Guid productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.InventoryMovements
            .AsNoTracking()
            .Where(movement => movement.ProductId == productId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(movement => movement.OccurredAt)
            .ThenByDescending(movement => movement.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new InventoryMovementPage(items, totalCount);
    }
}
