namespace SistemaGestion.Domain.Inventory;

public sealed class InsufficientStockException : InvalidOperationException
{
    public InsufficientStockException(decimal quantityOnHand, decimal quantityDelta)
        : base(
            $"The adjustment of {quantityDelta} cannot be applied to the current quantity " +
            $"of {quantityOnHand} because inventory cannot become negative.")
    {
        QuantityOnHand = quantityOnHand;
        QuantityDelta = quantityDelta;
    }

    public decimal QuantityOnHand { get; }

    public decimal QuantityDelta { get; }
}
