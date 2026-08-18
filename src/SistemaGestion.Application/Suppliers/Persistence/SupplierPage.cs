using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.Suppliers.Persistence;

public sealed record SupplierPage(
    IReadOnlyList<Supplier> Items,
    int TotalCount);
