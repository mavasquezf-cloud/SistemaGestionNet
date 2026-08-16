using System.ComponentModel.DataAnnotations;
using SistemaGestion.Application.Inventory;

namespace SistemaGestion.API.Contracts;

public sealed record ManualInventoryAdjustmentRequest(
    [property: NonZero] decimal QuantityDelta,
    [property: Required, StringLength(500, MinimumLength = 1)] string Reason,
    [property: StringLength(100)] string? Reference = null);

public sealed record InventoryAdjustmentResponse(
    Guid ProductId,
    decimal QuantityDelta,
    decimal QuantityOnHand,
    Guid MovementId,
    string Type,
    string Source,
    string? Reference,
    string Reason,
    DateTimeOffset OccurredAt);

public sealed record InventoryResponse(Guid ProductId, decimal QuantityOnHand);

public sealed record InventoryMovementResponse(
    Guid Id,
    Guid ProductId,
    decimal QuantityDelta,
    decimal ResultingBalance,
    string Type,
    string Source,
    string? Reference,
    string Reason,
    DateTimeOffset OccurredAt)
{
    public static InventoryMovementResponse FromResult(InventoryMovementResult result) => new(
        result.Id,
        result.ProductId,
        result.QuantityDelta,
        result.ResultingBalance,
        result.Type.ToString(),
        result.Source.ToString(),
        result.Reference,
        result.Reason,
        result.OccurredAt);
}

public sealed record PagedInventoryMovementsResponse(
    IReadOnlyList<InventoryMovementResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NonZeroAttribute : ValidationAttribute
{
    public NonZeroAttribute()
        : base("The {0} field must not be zero.")
    {
    }

    public override bool IsValid(object? value) => value is decimal number && number != 0m;
}
