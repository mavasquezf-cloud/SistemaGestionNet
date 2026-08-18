namespace SistemaGestion.Application.Suppliers.Persistence;

public sealed class SupplierDuplicateNumberException : Exception
{
    public SupplierDuplicateNumberException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
