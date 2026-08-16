namespace SistemaGestion.Application.Inventory.Persistence;

public sealed class InventoryConcurrencyException : Exception
{
    public InventoryConcurrencyException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
