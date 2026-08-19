using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Application.Purchasing.AddPurchaseLine;

public sealed record AddPurchaseLineCommand(Guid PurchaseId, Guid ProductId, decimal Quantity, decimal UnitCost);
public enum AddPurchaseLineOutcome { Success, PurchaseNotFound, PurchaseNotDraft, ProductNotFound, ProductInactive, DuplicateProduct, ConcurrencyConflict }
public sealed record AddPurchaseLineResult(AddPurchaseLineOutcome Outcome, PurchaseResult? Purchase);

public sealed class AddPurchaseLineUseCase(IPurchaseRepository purchases, IProductRepository products, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<AddPurchaseLineResult> ExecuteAsync(AddPurchaseLineCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var purchase = await purchases.GetByIdAsync(command.PurchaseId, cancellationToken);
        if (purchase is null) return new(AddPurchaseLineOutcome.PurchaseNotFound, null);
        if (purchase.Status != PurchaseStatus.Draft) return new(AddPurchaseLineOutcome.PurchaseNotDraft, null);
        var productResult = await products.GetByIdAsync(command.ProductId, cancellationToken);
        if (productResult is null) return new(AddPurchaseLineOutcome.ProductNotFound, null);
        if (productResult.Product.Status != ProductStatus.Active) return new(AddPurchaseLineOutcome.ProductInactive, null);
        if (purchase.Lines.Any(x => x.ProductId == command.ProductId)) return new(AddPurchaseLineOutcome.DuplicateProduct, null);
        purchase.AddLine(Guid.NewGuid(), productResult.Product.Id, productResult.Product.Name, productResult.Product.UnitOfMeasure, command.Quantity, command.UnitCost, clock.UtcNow);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (PurchaseConcurrencyException) { return new(AddPurchaseLineOutcome.ConcurrencyConflict, null); }
        return new(AddPurchaseLineOutcome.Success, PurchaseResult.FromPurchase(purchase));
    }
}
