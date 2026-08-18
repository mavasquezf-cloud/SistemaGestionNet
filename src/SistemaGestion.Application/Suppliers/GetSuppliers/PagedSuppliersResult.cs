namespace SistemaGestion.Application.Suppliers.GetSuppliers;

public sealed record PagedSuppliersResult(
    IReadOnlyList<SupplierResult> Items,
    int Page,
    int PageSize,
    int TotalCount);
