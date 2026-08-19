using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Application.Suppliers.Persistence;
using SistemaGestion.Domain.Purchasing;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.Purchasing.CreatePurchase;

public sealed record CreatePurchaseCommand(Guid SupplierId, string? SupplierDocumentReference = null);
public enum CreatePurchaseOutcome { Success, SupplierNotFound, SupplierInactive, DuplicatePurchaseNumber }
public sealed record CreatePurchaseResult(CreatePurchaseOutcome Outcome, PurchaseResult? Purchase);

public sealed class CreatePurchaseUseCase(ISupplierRepository suppliers, IPurchaseRepository purchases, IPurchaseNumberGenerator numbers, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<CreatePurchaseResult> ExecuteAsync(CreatePurchaseCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var supplier = await suppliers.GetByIdAsync(command.SupplierId, cancellationToken);
        if (supplier is null) return new(CreatePurchaseOutcome.SupplierNotFound, null);
        if (supplier.Status != SupplierStatus.Active) return new(CreatePurchaseOutcome.SupplierInactive, null);
        var purchase = new Purchase(Guid.NewGuid(), await numbers.NextAsync(cancellationToken), supplier.Id, supplier.Name, clock.UtcNow, command.SupplierDocumentReference);
        await purchases.AddAsync(purchase, cancellationToken);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (PurchaseDuplicateNumberException) { return new(CreatePurchaseOutcome.DuplicatePurchaseNumber, null); }
        return new(CreatePurchaseOutcome.Success, PurchaseResult.FromPurchase(purchase));
    }
}
