using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Application.Customers;

public sealed record CustomerResult(Guid Id, string CustomerNumber, string Name,
    string? TaxIdentificationNumber, string? Email, string? Phone, string? Address,
    CustomerStatus Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static CustomerResult FromCustomer(Customer customer) => new(
        customer.Id, customer.CustomerNumber.Value, customer.Name,
        customer.TaxIdentificationNumber, customer.Email, customer.Phone, customer.Address,
        customer.Status, customer.CreatedAt, customer.UpdatedAt);
}
