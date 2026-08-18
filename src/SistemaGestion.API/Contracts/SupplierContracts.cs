using System.ComponentModel.DataAnnotations;
using SistemaGestion.Application.Suppliers;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.API.Contracts;

public sealed record CreateSupplierRequest(
    [property: Required, StringLength(50, MinimumLength = 1)] string SupplierNumber,
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: StringLength(50)] string? TaxIdentificationNumber = null,
    [property: EmailAddress, StringLength(254)] string? Email = null,
    [property: StringLength(50)] string? Phone = null,
    [property: StringLength(500)] string? Address = null);

public sealed record SupplierResponse(
    Guid Id,
    string SupplierNumber,
    string Name,
    string? TaxIdentificationNumber,
    string? Email,
    string? Phone,
    string? Address,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static SupplierResponse FromResult(SupplierResult result) => new(
        result.Id,
        result.SupplierNumber,
        result.Name,
        result.TaxIdentificationNumber,
        result.Email,
        result.Phone,
        result.Address,
        result.Status.ToString(),
        result.CreatedAt,
        result.UpdatedAt);
}

public sealed record PagedSuppliersResponse(
    IReadOnlyList<SupplierResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ChangeSupplierStatusRequest(
    [property: Required]
    [property: AllowedValues(nameof(SupplierStatus.Active), nameof(SupplierStatus.Inactive))]
    string Status);
