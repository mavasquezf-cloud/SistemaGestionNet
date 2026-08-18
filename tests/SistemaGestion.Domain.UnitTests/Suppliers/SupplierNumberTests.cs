using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Domain.UnitTests.Suppliers;

public sealed class SupplierNumberTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingValue_ThrowsArgumentException(string? value)
    {
        Assert.Throws<ArgumentException>(() => new SupplierNumber(value!));
    }

    [Fact]
    public void Constructor_TrimsAndUppercasesValue()
    {
        var supplierNumber = new SupplierNumber("  sup-001_a.b  ");

        Assert.Equal("SUP-001_A.B", supplierNumber.Value);
        Assert.Equal("SUP-001_A.B", supplierNumber.ToString());
    }

    [Fact]
    public void Constructor_WithValueOverMaximumLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new SupplierNumber(new string('A', SupplierNumber.MaximumLength + 1)));
    }

    [Theory]
    [InlineData("SUP 001")]
    [InlineData("SUP/001")]
    [InlineData("SUP@001")]
    [InlineData("SUP-Á")]
    public void Constructor_WithInvalidCharacters_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => new SupplierNumber(value));
    }

    [Fact]
    public void EqualNormalizedValues_HaveValueEquality()
    {
        var first = new SupplierNumber("sup-001");
        var second = new SupplierNumber(" SUP-001 ");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
