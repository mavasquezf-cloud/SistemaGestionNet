namespace SistemaGestion.Application.Suppliers.GetSupplierById;

public sealed record GetSupplierByIdResult(
    bool Found,
    SupplierResult? Supplier);
