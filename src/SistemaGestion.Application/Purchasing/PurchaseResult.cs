using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Application.Purchasing;

public sealed record PurchaseLineResult(Guid Id, Guid ProductId, string ProductName, string UnitOfMeasure, decimal Quantity, decimal UnitCost, decimal LineTotal);

public sealed record PurchaseResult(Guid Id, string PurchaseNumber, Guid SupplierId, string SupplierName, string? SupplierDocumentReference, PurchaseStatus Status, decimal Total, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ReceivedAt, IReadOnlyCollection<PurchaseLineResult> Lines)
{
    public static PurchaseResult FromPurchase(Purchase purchase) => new(
        purchase.Id, purchase.PurchaseNumber.Value, purchase.SupplierId, purchase.SupplierName,
        purchase.SupplierDocumentReference, purchase.Status, purchase.Total, purchase.CreatedAt,
        purchase.UpdatedAt, purchase.ReceivedAt,
        purchase.Lines.Select(line => new PurchaseLineResult(line.Id, line.ProductId, line.ProductName,
            line.UnitOfMeasure, line.Quantity, line.UnitCost, line.LineTotal)).ToArray());
}
