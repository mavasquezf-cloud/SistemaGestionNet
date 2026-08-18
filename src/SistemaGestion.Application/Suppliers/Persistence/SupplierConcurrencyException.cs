namespace SistemaGestion.Application.Suppliers.Persistence;

public sealed class SupplierConcurrencyException : Exception
{
    public SupplierConcurrencyException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
