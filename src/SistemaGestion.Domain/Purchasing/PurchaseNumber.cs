namespace SistemaGestion.Domain.Purchasing;

public sealed record PurchaseNumber
{
    public const int MaximumLength = 50;

    public PurchaseNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Purchase number is required.", nameof(value));
        }

        var normalizedValue = value.Trim().ToUpperInvariant();
        if (normalizedValue.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"Purchase number cannot exceed {MaximumLength} characters.", nameof(value));
        }

        if (normalizedValue.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                "Purchase number can contain only ASCII letters, digits, hyphens, underscores, and periods.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
