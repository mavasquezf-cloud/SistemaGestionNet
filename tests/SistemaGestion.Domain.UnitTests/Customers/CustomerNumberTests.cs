using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Domain.UnitTests.Customers;

public sealed class CustomerNumberTests
{
    [Theory]
    [InlineData(" cus-001 ", "CUS-001")]
    [InlineData("customer_01", "CUSTOMER_01")]
    [InlineData("abc", "ABC")]
    [InlineData("123", "123")]
    [InlineData("A-B_C.D", "A-B_C.D")]
    public void Constructor_NormalizesValidValues(string input, string expected)
    {
        var number = new CustomerNumber(input);
        Assert.Equal(expected, number.Value);
        Assert.Equal(expected, number.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingValue_ThrowsArgumentException(string? value) =>
        Assert.Throws<ArgumentException>(() => new CustomerNumber(value!));

    [Theory]
    [InlineData("CUS 001")]
    [InlineData("CUS/001")]
    [InlineData("CUS@001")]
    [InlineData("CUS-Á")]
    public void Constructor_WithUnsupportedCharacters_ThrowsArgumentException(string value) =>
        Assert.Throws<ArgumentException>(() => new CustomerNumber(value));

    [Fact]
    public void Constructor_AcceptsMaximumLength() =>
        Assert.Equal(CustomerNumber.MaximumLength,
            new CustomerNumber(new string('A', CustomerNumber.MaximumLength)).Value.Length);

    [Fact]
    public void Constructor_OverMaximumLength_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            new CustomerNumber(new string('A', CustomerNumber.MaximumLength + 1)));

    [Fact]
    public void EqualNormalizedValues_HaveValueEquality()
    {
        var first = new CustomerNumber("cus-001");
        var second = new CustomerNumber(" CUS-001 ");
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
