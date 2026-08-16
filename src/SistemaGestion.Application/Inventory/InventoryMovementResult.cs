using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Application.Inventory;

public sealed record InventoryMovementResult(
    Guid Id,
    Guid ProductId,
    decimal QuantityDelta,
    decimal ResultingBalance,
    InventoryMovementType Type,
    MovementSource Source,
    string? Reference,
    string Reason,
    DateTimeOffset OccurredAt)
{
    internal static InventoryMovementResult FromMovement(InventoryMovement movement) => new(
        movement.Id,
        movement.ProductId,
        movement.QuantityDelta,
        movement.ResultingBalance,
        movement.Type,
        movement.Source,
        movement.Reference,
        movement.Reason,
        movement.OccurredAt);
}
