using System.ComponentModel.DataAnnotations;
using SistemaGestion.Application.Customers;
using SistemaGestion.Domain.Customers;

namespace SistemaGestion.API.Contracts;

public sealed record CreateCustomerRequest(
    [property: Required, StringLength(50, MinimumLength = 1)] string CustomerNumber,
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: StringLength(50)] string? TaxIdentificationNumber = null,
    [property: EmailAddress, StringLength(254)] string? Email = null,
    [property: StringLength(50)] string? Phone = null,
    [property: StringLength(500)] string? Address = null);

public sealed record CustomerResponse(
    Guid Id,
    string CustomerNumber,
    string Name,
    string? TaxIdentificationNumber,
    string? Email,
    string? Phone,
    string? Address,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CustomerResponse FromResult(CustomerResult result) => new(
        result.Id,
        result.CustomerNumber,
        result.Name,
        result.TaxIdentificationNumber,
        result.Email,
        result.Phone,
        result.Address,
        result.Status switch
        {
            CustomerStatus.Active => "Active",
            CustomerStatus.Inactive => "Inactive",
            _ => throw new ArgumentOutOfRangeException(
                nameof(result), result.Status, "Customer status is not supported.")
        },
        result.CreatedAt,
        result.UpdatedAt);
}

public sealed record PagedCustomersResponse(
    IReadOnlyList<CustomerResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ChangeCustomerStatusRequest(
    [property: Required]
    [property: AllowedValues(nameof(CustomerStatus.Active), nameof(CustomerStatus.Inactive))]
    string Status);
