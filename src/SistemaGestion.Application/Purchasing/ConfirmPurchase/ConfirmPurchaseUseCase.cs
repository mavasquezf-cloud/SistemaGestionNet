using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Application.Purchasing.ConfirmPurchase;

public enum ConfirmPurchaseOutcome { Success, PurchaseNotFound, EmptyPurchase, InvalidStatus, ConcurrencyConflict }
public sealed record ConfirmPurchaseResult(ConfirmPurchaseOutcome Outcome, PurchaseResult? Purchase);
public sealed class ConfirmPurchaseUseCase(IPurchaseRepository purchases, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<ConfirmPurchaseResult> ExecuteAsync(Guid purchaseId, CancellationToken cancellationToken = default)
    {
        var purchase = await purchases.GetByIdAsync(purchaseId, cancellationToken);
        if (purchase is null) return new(ConfirmPurchaseOutcome.PurchaseNotFound, null);
        if (purchase.Status != PurchaseStatus.Draft) return new(ConfirmPurchaseOutcome.InvalidStatus, null);
        if (purchase.Lines.Count == 0) return new(ConfirmPurchaseOutcome.EmptyPurchase, null);
        purchase.Confirm(clock.UtcNow);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (PurchaseConcurrencyException) { return new(ConfirmPurchaseOutcome.ConcurrencyConflict, null); }
        return new(ConfirmPurchaseOutcome.Success, PurchaseResult.FromPurchase(purchase));
    }
}
