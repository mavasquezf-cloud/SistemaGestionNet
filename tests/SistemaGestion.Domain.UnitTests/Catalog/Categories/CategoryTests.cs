using SistemaGestion.Domain.Catalog.Categories;

namespace SistemaGestion.Domain.UnitTests.Catalog.Categories;

public sealed class CategoryTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesActiveCategory()
    {
        var id = Guid.NewGuid();

        var category = new Category(id, "  Electronics  ", "  Electronic products  ");

        Assert.Equal(id, category.Id);
        Assert.Equal("Electronics", category.Name);
        Assert.Equal("Electronic products", category.Description);
        Assert.True(category.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingName_ThrowsArgumentException(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Category(Guid.NewGuid(), name!));
    }

    [Fact]
    public void ActivateAndDeactivate_ChangeActiveState()
    {
        var category = new Category(Guid.NewGuid(), "Electronics");

        category.Deactivate();
        Assert.False(category.IsActive);

        category.Activate();
        Assert.True(category.IsActive);
    }
}
