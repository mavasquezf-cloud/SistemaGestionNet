namespace SistemaGestion.Application.Suppliers.CreateSupplier;

public sealed record CreateSupplierCommand(
    string SupplierNumber,
    string Name,
    string? TaxIdentificationNumber = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null);
