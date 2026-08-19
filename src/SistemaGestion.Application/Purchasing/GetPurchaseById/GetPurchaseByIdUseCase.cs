using SistemaGestion.Application.Purchasing.Persistence;

namespace SistemaGestion.Application.Purchasing.GetPurchaseById;

public enum GetPurchaseByIdOutcome { Found, NotFound }
public sealed record GetPurchaseByIdResult(GetPurchaseByIdOutcome Outcome, PurchaseResult? Purchase);
public sealed class GetPurchaseByIdUseCase(IPurchaseRepository purchases)
{
    public async Task<GetPurchaseByIdResult> ExecuteAsync(Guid purchaseId, CancellationToken cancellationToken = default)
    {
        var purchase = await purchases.GetByIdAsync(purchaseId, cancellationToken);
        return purchase is null ? new(GetPurchaseByIdOutcome.NotFound, null) : new(GetPurchaseByIdOutcome.Found, PurchaseResult.FromPurchase(purchase));
    }
}
