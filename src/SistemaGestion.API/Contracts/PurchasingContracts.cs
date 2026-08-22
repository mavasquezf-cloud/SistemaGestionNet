using System.ComponentModel.DataAnnotations;
using SistemaGestion.Application.Purchasing;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.API.Contracts;

public sealed record CreatePurchaseRequest(
    [property: Required] Guid SupplierId,
    [property: StringLength(100)] string? SupplierDocumentReference = null);

public sealed record AddPurchaseLineRequest(
    [property: Required] Guid ProductId,
    [property: Range(0.0001, double.MaxValue)] decimal Quantity,
    [property: Range(0, double.MaxValue)] decimal UnitCost);

public sealed record PurchaseLineResponse(Guid Id, Guid ProductId, string ProductName,
    string UnitOfMeasure, decimal Quantity, decimal UnitCost, decimal LineTotal)
{
    public static PurchaseLineResponse FromResult(PurchaseLineResult result) => new(
        result.Id, result.ProductId, result.ProductName, result.UnitOfMeasure,
        result.Quantity, result.UnitCost, result.LineTotal);
}

public sealed record PurchaseResponse(Guid Id, string PurchaseNumber, Guid SupplierId,
    string SupplierName, string? SupplierDocumentReference, string Status, decimal Total,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ReceivedAt,
    IReadOnlyList<PurchaseLineResponse> Lines)
{
    public static PurchaseResponse FromResult(PurchaseResult result) => new(
        result.Id, result.PurchaseNumber, result.SupplierId, result.SupplierName,
        result.SupplierDocumentReference, ToStatusString(result.Status), result.Total,
        result.CreatedAt, result.UpdatedAt, result.ReceivedAt,
        result.Lines.Select(PurchaseLineResponse.FromResult).ToArray());

    private static string ToStatusString(PurchaseStatus status) => status switch
    {
        PurchaseStatus.Draft => nameof(PurchaseStatus.Draft),
        PurchaseStatus.Confirmed => nameof(PurchaseStatus.Confirmed),
        PurchaseStatus.Received => nameof(PurchaseStatus.Received),
        PurchaseStatus.Cancelled => nameof(PurchaseStatus.Cancelled),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown purchase status.")
    };
}

public sealed record PagedPurchasesResponse(IReadOnlyList<PurchaseResponse> Items,
    int Page, int PageSize, int TotalCount);
