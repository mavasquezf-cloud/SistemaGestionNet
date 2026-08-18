namespace SistemaGestion.Domain.Purchasing;

public sealed class PurchaseLine
{
    public const int MaximumProductNameLength = 200;
    public const int MaximumUnitOfMeasureLength = 50;

    internal PurchaseLine(
        Guid id,
        Guid purchaseId,
        Guid productId,
        string productName,
        string unitOfMeasure,
        decimal quantity,
        decimal unitCost)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Purchase line ID cannot be empty.", nameof(id));
        }

        if (purchaseId == Guid.Empty)
        {
            throw new ArgumentException("Purchase ID cannot be empty.", nameof(purchaseId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Product name is required.", nameof(productName));
        }

        var normalizedProductName = productName.Trim();
        if (normalizedProductName.Length > MaximumProductNameLength)
        {
            throw new ArgumentException(
                $"Product name cannot exceed {MaximumProductNameLength} characters.", nameof(productName));
        }

        if (string.IsNullOrWhiteSpace(unitOfMeasure))
        {
            throw new ArgumentException("Unit of measure is required.", nameof(unitOfMeasure));
        }

        var normalizedUnitOfMeasure = unitOfMeasure.Trim();
        if (normalizedUnitOfMeasure.Length > MaximumUnitOfMeasureLength)
        {
            throw new ArgumentException(
                $"Unit of measure cannot exceed {MaximumUnitOfMeasureLength} characters.", nameof(unitOfMeasure));
        }

        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be greater than zero.");
        }

        if (unitCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost), unitCost, "Unit cost cannot be negative.");
        }

        Id = id;
        PurchaseId = purchaseId;
        ProductId = productId;
        ProductName = normalizedProductName;
        UnitOfMeasure = normalizedUnitOfMeasure;
        Quantity = quantity;
        UnitCost = unitCost;
        LineTotal = decimal.Round(quantity * unitCost, 4, MidpointRounding.AwayFromZero);
    }

    public Guid Id { get; }
    public Guid PurchaseId { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string UnitOfMeasure { get; }
    public decimal Quantity { get; }
    public decimal UnitCost { get; }
    public decimal LineTotal { get; }
}
