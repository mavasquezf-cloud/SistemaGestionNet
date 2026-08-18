namespace SistemaGestion.Application.Suppliers.CreateSupplier;

public sealed record CreateSupplierResult(
    CreateSupplierOutcome Outcome,
    SupplierResult? Supplier);
