using System.Net.Mail;

namespace SistemaGestion.Domain.Suppliers;

public sealed class Supplier
{
    public const int MaximumNameLength = 200;
    public const int MaximumTaxIdentificationNumberLength = 50;
    public const int MaximumEmailLength = 254;
    public const int MaximumPhoneLength = 50;
    public const int MaximumAddressLength = 500;

    public Supplier(
        Guid id,
        SupplierNumber supplierNumber,
        string name,
        DateTimeOffset createdAt,
        string? taxIdentificationNumber = null,
        string? email = null,
        string? phone = null,
        string? address = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Supplier ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(supplierNumber);

        Id = id;
        SupplierNumber = supplierNumber;
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name), "Supplier name");
        TaxIdentificationNumber = NormalizeOptional(
            taxIdentificationNumber,
            MaximumTaxIdentificationNumberLength,
            nameof(taxIdentificationNumber),
            "Tax identification number");
        Email = NormalizeEmail(email);
        Phone = NormalizeOptional(phone, MaximumPhoneLength, nameof(phone), "Phone");
        Address = NormalizeOptional(address, MaximumAddressLength, nameof(address), "Address");
        Status = SupplierStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public SupplierNumber SupplierNumber { get; }

    public string Name { get; }

    public string? TaxIdentificationNumber { get; }

    public string? Email { get; }

    public string? Phone { get; }

    public string? Address { get; }

    public SupplierStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Activate(DateTimeOffset occurredAt)
    {
        if (Status == SupplierStatus.Active)
        {
            return;
        }

        Status = SupplierStatus.Active;
        UpdatedAt = occurredAt;
    }

    public void Deactivate(DateTimeOffset occurredAt)
    {
        if (Status == SupplierStatus.Inactive)
        {
            return;
        }

        Status = SupplierStatus.Inactive;
        UpdatedAt = occurredAt;
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", parameterName);
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{fieldName} cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalizedValue;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string parameterName,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{fieldName} cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalizedValue;
    }

    private static string? NormalizeEmail(string? email)
    {
        var normalizedEmail = NormalizeOptional(
            email, MaximumEmailLength, nameof(email), "Email");
        if (normalizedEmail is null)
        {
            return null;
        }

        if (!MailAddress.TryCreate(normalizedEmail, out var parsedEmail)
            || !string.Equals(parsedEmail.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Email is not valid.", nameof(email));
        }

        return normalizedEmail;
    }
}
