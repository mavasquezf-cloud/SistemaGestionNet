using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Application.Inventory.AdjustInventory;

public sealed class AdjustInventoryUseCase(
    IProductRepository productRepository,
    IInventoryItemRepository inventoryItemRepository,
    IInventoryMovementRepository inventoryMovementRepository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<AdjustInventoryResult> ExecuteAsync(
        AdjustInventoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var productWithCategory = await productRepository.GetByIdAsync(
            command.ProductId, cancellationToken);
        if (productWithCategory is null)
        {
            return Rejected(AdjustInventoryOutcome.ProductNotFound);
        }

        if (productWithCategory.Product.Status != ProductStatus.Active)
        {
            return Rejected(AdjustInventoryOutcome.ProductInactive);
        }

        var inventoryItem = await inventoryItemRepository.GetByProductIdAsync(
            command.ProductId, cancellationToken);
        var isNew = inventoryItem is null;
        inventoryItem ??= new InventoryItem(Guid.NewGuid(), command.ProductId);

        InventoryMovement movement;
        try
        {
            movement = inventoryItem.ApplyManualAdjustment(
                Guid.NewGuid(),
                command.QuantityDelta,
                command.Reason,
                command.Reference,
                clock.UtcNow);
        }
        catch (InsufficientStockException)
        {
            return Rejected(AdjustInventoryOutcome.InsufficientStock);
        }

        if (isNew)
        {
            await inventoryItemRepository.AddAsync(inventoryItem, cancellationToken);
        }

        await inventoryMovementRepository.AddAsync(movement, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (InventoryConcurrencyException)
        {
            return Rejected(AdjustInventoryOutcome.ConcurrencyConflict);
        }

        return new AdjustInventoryResult(
            AdjustInventoryOutcome.Success,
            inventoryItem.QuantityOnHand,
            InventoryMovementResult.FromMovement(movement));
    }

    private static AdjustInventoryResult Rejected(AdjustInventoryOutcome outcome) =>
        new(outcome, null, null);
}
