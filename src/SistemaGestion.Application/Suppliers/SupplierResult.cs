using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.Suppliers;

public sealed record SupplierResult(
    Guid Id,
    string SupplierNumber,
    string Name,
    string? TaxIdentificationNumber,
    string? Email,
    string? Phone,
    string? Address,
    SupplierStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static SupplierResult FromSupplier(Supplier supplier) => new(
        supplier.Id,
        supplier.SupplierNumber.Value,
        supplier.Name,
        supplier.TaxIdentificationNumber,
        supplier.Email,
        supplier.Phone,
        supplier.Address,
        supplier.Status,
        supplier.CreatedAt,
        supplier.UpdatedAt);
}
