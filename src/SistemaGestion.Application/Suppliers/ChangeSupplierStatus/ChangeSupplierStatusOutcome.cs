namespace SistemaGestion.Application.Suppliers.ChangeSupplierStatus;

public enum ChangeSupplierStatusOutcome
{
    Success = 1,
    SupplierNotFound = 2,
    ConcurrencyConflict = 3
}
