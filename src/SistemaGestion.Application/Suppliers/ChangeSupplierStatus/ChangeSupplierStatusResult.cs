namespace SistemaGestion.Application.Suppliers.ChangeSupplierStatus;

public sealed record ChangeSupplierStatusResult(
    ChangeSupplierStatusOutcome Outcome,
    SupplierResult? Supplier);
