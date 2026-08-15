using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Domain.UnitTests.Catalog.Products;

public sealed class SkuTests
{
    [Fact]
    public void Constructor_NormalizesValue()
    {
        var sku = new Sku("  abc-123  ");

        Assert.Equal("ABC-123", sku.Value);
        Assert.Equal("ABC-123", sku.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidValue_ThrowsArgumentException(string? value)
    {
        Assert.Throws<ArgumentException>(() => new Sku(value!));
    }
}
