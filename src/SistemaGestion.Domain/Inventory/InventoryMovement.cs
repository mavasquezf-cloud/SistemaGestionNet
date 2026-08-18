namespace SistemaGestion.Domain.Inventory;

public sealed class InventoryMovement
{
    internal InventoryMovement(
        Guid id,
        Guid inventoryItemId,
        Guid productId,
        decimal quantityDelta,
        decimal resultingBalance,
        MovementSource source,
        string reason,
        string? reference,
        DateTimeOffset occurredAt)
    {
        Id = id;
        InventoryItemId = inventoryItemId;
        ProductId = productId;
        QuantityDelta = quantityDelta;
        ResultingBalance = resultingBalance;
        Type = quantityDelta > 0
            ? InventoryMovementType.Increase
            : InventoryMovementType.Decrease;
        Source = source;
        Reference = reference;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; }

    public Guid InventoryItemId { get; }

    public Guid ProductId { get; }

    public decimal QuantityDelta { get; }

    public decimal ResultingBalance { get; }

    public InventoryMovementType Type { get; }

    public MovementSource Source { get; }

    public string? Reference { get; }

    public string Reason { get; }

    public DateTimeOffset OccurredAt { get; }
}
