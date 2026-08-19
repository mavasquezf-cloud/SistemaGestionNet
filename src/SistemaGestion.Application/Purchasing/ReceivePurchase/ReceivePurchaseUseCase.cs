using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Domain.Inventory;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Application.Purchasing.ReceivePurchase;

public enum ReceivePurchaseOutcome { Success, PurchaseNotFound, PurchaseNotConfirmed, AlreadyReceived, ConcurrencyConflict }
public sealed record ReceivePurchaseResult(ReceivePurchaseOutcome Outcome, PurchaseResult? Purchase);
public sealed class ReceivePurchaseUseCase(IPurchaseRepository purchases, IInventoryItemRepository inventoryItems, IInventoryMovementRepository movements, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<ReceivePurchaseResult> ExecuteAsync(Guid purchaseId, CancellationToken cancellationToken = default)
    {
        var purchase = await purchases.GetByIdAsync(purchaseId, cancellationToken);
        if (purchase is null) return new(ReceivePurchaseOutcome.PurchaseNotFound, null);
        if (purchase.Status == PurchaseStatus.Received) return new(ReceivePurchaseOutcome.AlreadyReceived, null);
        if (purchase.Status != PurchaseStatus.Confirmed) return new(ReceivePurchaseOutcome.PurchaseNotConfirmed, null);
        var productIds = purchase.Lines.Select(x => x.ProductId).Distinct().ToArray();
        var loaded = await inventoryItems.GetByProductIdsAsync(productIds, cancellationToken);
        var occurredAt = clock.UtcNow;
        foreach (var line in purchase.Lines)
        {
            if (!loaded.TryGetValue(line.ProductId, out var item))
            {
                item = new InventoryItem(Guid.NewGuid(), line.ProductId);
                await inventoryItems.AddAsync(item, cancellationToken);
            }
            var movement = item.ApplyPurchaseReceipt(Guid.NewGuid(), line.Quantity, purchase.PurchaseNumber.Value, $"Purchase receipt {purchase.PurchaseNumber.Value}", occurredAt);
            await movements.AddAsync(movement, cancellationToken);
        }
        purchase.Receive(occurredAt);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (Exception exception) when (exception is PurchaseConcurrencyException or InventoryConcurrencyException or PurchaseReceiptConflictException)
        { return new(ReceivePurchaseOutcome.ConcurrencyConflict, null); }
        return new(ReceivePurchaseOutcome.Success, PurchaseResult.FromPurchase(purchase));
    }
}
