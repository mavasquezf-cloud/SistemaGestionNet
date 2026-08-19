using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Application.Purchasing.CancelPurchase;

public enum CancelPurchaseOutcome { Success, PurchaseNotFound, InvalidStatus, ConcurrencyConflict }
public sealed record CancelPurchaseResult(CancelPurchaseOutcome Outcome, PurchaseResult? Purchase);
public sealed class CancelPurchaseUseCase(IPurchaseRepository purchases, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<CancelPurchaseResult> ExecuteAsync(Guid purchaseId, CancellationToken cancellationToken = default)
    {
        var purchase = await purchases.GetByIdAsync(purchaseId, cancellationToken);
        if (purchase is null) return new(CancelPurchaseOutcome.PurchaseNotFound, null);
        if (purchase.Status is not PurchaseStatus.Draft and not PurchaseStatus.Confirmed) return new(CancelPurchaseOutcome.InvalidStatus, null);
        purchase.Cancel(clock.UtcNow);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (PurchaseConcurrencyException) { return new(CancelPurchaseOutcome.ConcurrencyConflict, null); }
        return new(CancelPurchaseOutcome.Success, PurchaseResult.FromPurchase(purchase));
    }
}
