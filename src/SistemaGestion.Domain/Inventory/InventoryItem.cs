namespace SistemaGestion.Domain.Inventory;

public sealed class InventoryItem
{
    public const int MaximumReasonLength = 500;
    public const int MaximumReferenceLength = 100;

    public InventoryItem(Guid id, Guid productId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Inventory item ID cannot be empty.", nameof(id));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        }

        Id = id;
        ProductId = productId;
        QuantityOnHand = 0m;
        RowVersion = [];
    }

    public Guid Id { get; }

    public Guid ProductId { get; }

    public decimal QuantityOnHand { get; private set; }

    public byte[] RowVersion { get; private set; }

    public InventoryMovement ApplyManualAdjustment(
        Guid movementId,
        decimal quantityDelta,
        string reason,
        string? reference,
        DateTimeOffset occurredAt)
    {
        if (movementId == Guid.Empty)
        {
            throw new ArgumentException("Inventory movement ID cannot be empty.", nameof(movementId));
        }

        if (quantityDelta == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityDelta), quantityDelta, "Quantity delta cannot be zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Adjustment reason is required.", nameof(reason));
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > MaximumReasonLength)
        {
            throw new ArgumentException(
                $"Adjustment reason cannot exceed {MaximumReasonLength} characters.", nameof(reason));
        }

        var normalizedReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (normalizedReference?.Length > MaximumReferenceLength)
        {
            throw new ArgumentException(
                $"Adjustment reference cannot exceed {MaximumReferenceLength} characters.", nameof(reference));
        }

        var resultingBalance = QuantityOnHand + quantityDelta;
        if (resultingBalance < 0m)
        {
            throw new InsufficientStockException(QuantityOnHand, quantityDelta);
        }

        QuantityOnHand = resultingBalance;

        return new InventoryMovement(
            movementId,
            Id,
            ProductId,
            quantityDelta,
            resultingBalance,
            normalizedReason,
            normalizedReference,
            occurredAt);
    }
}
