using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Domain.UnitTests.Catalog.Products;

public sealed class ProductTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesActiveProduct()
    {
        var id = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var sku = new Sku("laptop-001");

        var product = new Product(
            id,
            sku,
            "  Business Laptop  ",
            categoryId,
            "  Unit  ",
            1299.99m,
            "  Professional laptop  ");

        Assert.Equal(id, product.Id);
        Assert.Same(sku, product.Sku);
        Assert.Equal("Business Laptop", product.Name);
        Assert.Equal("Professional laptop", product.Description);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal("Unit", product.UnitOfMeasure);
        Assert.Equal(1299.99m, product.DefaultSalePrice);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public void Constructor_WithNegativeSalePrice_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateProduct(defaultSalePrice: -0.01m));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingName_ThrowsArgumentException(string? name)
    {
        Assert.Throws<ArgumentException>(() => CreateProduct(name: name!));
    }

    [Fact]
    public void Constructor_WithEmptyCategoryId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CreateProduct(categoryId: Guid.Empty));
    }

    [Fact]
    public void ActivateAndDeactivate_ChangeStatus()
    {
        var product = CreateProduct();

        product.Deactivate();
        Assert.Equal(ProductStatus.Inactive, product.Status);

        product.Activate();
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    private static Product CreateProduct(
        string name = "Business Laptop",
        Guid? categoryId = null,
        decimal defaultSalePrice = 1299.99m)
    {
        return new Product(
            Guid.NewGuid(),
            new Sku("LAPTOP-001"),
            name,
            categoryId ?? Guid.NewGuid(),
            "Unit",
            defaultSalePrice);
    }
}
