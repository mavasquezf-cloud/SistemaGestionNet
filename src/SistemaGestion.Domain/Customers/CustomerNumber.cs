namespace SistemaGestion.Domain.Customers;

public sealed record CustomerNumber
{
    public const int MaximumLength = 50;

    public CustomerNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Customer number is required.", nameof(value));
        }

        var normalizedValue = value.Trim().ToUpperInvariant();
        if (normalizedValue.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"Customer number cannot exceed {MaximumLength} characters.", nameof(value));
        }

        if (normalizedValue.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                "Customer number can contain only letters, digits, hyphens, underscores, and periods.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
