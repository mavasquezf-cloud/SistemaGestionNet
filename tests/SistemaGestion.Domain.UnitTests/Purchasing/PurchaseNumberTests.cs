using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Domain.UnitTests.Purchasing;

public sealed class PurchaseNumberTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingValue_ThrowsArgumentException(string? value)
    {
        Assert.Throws<ArgumentException>(() => new PurchaseNumber(value!));
    }

    [Fact]
    public void Constructor_TrimsAndUppercasesValue()
    {
        var number = new PurchaseNumber("  pur-00000001  ");

        Assert.Equal("PUR-00000001", number.Value);
        Assert.Equal("PUR-00000001", number.ToString());
    }

    [Fact]
    public void Constructor_WithValueOverMaximumLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new PurchaseNumber(new string('A', PurchaseNumber.MaximumLength + 1)));
    }

    [Theory]
    [InlineData("PUR 001")]
    [InlineData("PUR/001")]
    [InlineData("PÜR-001")]
    public void Constructor_WithInvalidCharacters_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => new PurchaseNumber(value));
    }

    [Fact]
    public void EqualNormalizedValues_HaveValueEquality()
    {
        Assert.Equal(new PurchaseNumber("pur-001"), new PurchaseNumber(" PUR-001 "));
    }
}
