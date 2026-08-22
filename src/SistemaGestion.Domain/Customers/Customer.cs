using System.Net.Mail;

namespace SistemaGestion.Domain.Customers;

public sealed class Customer
{
    public const int MaximumNameLength = 200;
    public const int MaximumTaxIdentificationNumberLength = 50;
    public const int MaximumEmailLength = 254;
    public const int MaximumPhoneLength = 50;
    public const int MaximumAddressLength = 500;

    public Customer(
        Guid id,
        CustomerNumber customerNumber,
        string name,
        DateTimeOffset createdAt,
        string? taxIdentificationNumber = null,
        string? email = null,
        string? phone = null,
        string? address = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Customer ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(customerNumber);

        Id = id;
        CustomerNumber = customerNumber;
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name), "Customer name");
        TaxIdentificationNumber = NormalizeOptional(
            taxIdentificationNumber, MaximumTaxIdentificationNumberLength,
            nameof(taxIdentificationNumber), "Tax identification number");
        Email = NormalizeEmail(email);
        Phone = NormalizeOptional(phone, MaximumPhoneLength, nameof(phone), "Phone");
        Address = NormalizeOptional(address, MaximumAddressLength, nameof(address), "Address");
        Status = CustomerStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }
    public CustomerNumber CustomerNumber { get; }
    public string Name { get; }
    public string? TaxIdentificationNumber { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public string? Address { get; }
    public CustomerStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Activate(DateTimeOffset occurredAt)
    {
        if (Status == CustomerStatus.Active) return;
        Status = CustomerStatus.Active;
        UpdatedAt = occurredAt;
    }

    public void Deactivate(DateTimeOffset occurredAt)
    {
        if (Status == CustomerStatus.Inactive) return;
        Status = CustomerStatus.Inactive;
        UpdatedAt = occurredAt;
    }

    private static string NormalizeRequired(
        string value, int maximumLength, string parameterName, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"{fieldName} cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }

    private static string? NormalizeOptional(
        string? value, int maximumLength, string parameterName, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"{fieldName} cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }

    private static string? NormalizeEmail(string? email)
    {
        var normalized = NormalizeOptional(email, MaximumEmailLength, nameof(email), "Email");
        if (normalized is null) return null;
        if (!MailAddress.TryCreate(normalized, out var parsed)
            || !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Email is not valid.", nameof(email));
        return normalized;
    }
}
