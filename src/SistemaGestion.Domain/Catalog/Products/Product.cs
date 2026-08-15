namespace SistemaGestion.Domain.Catalog.Products;

public sealed class Product
{
    public Product(
        Guid id,
        Sku sku,
        string name,
        Guid categoryId,
        string unitOfMeasure,
        decimal defaultSalePrice,
        string? description = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(sku);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category ID cannot be empty.", nameof(categoryId));
        }

        if (string.IsNullOrWhiteSpace(unitOfMeasure))
        {
            throw new ArgumentException("Unit of measure is required.", nameof(unitOfMeasure));
        }

        if (defaultSalePrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultSalePrice),
                defaultSalePrice,
                "Default sale price cannot be negative.");
        }

        Id = id;
        Sku = sku;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CategoryId = categoryId;
        UnitOfMeasure = unitOfMeasure.Trim();
        DefaultSalePrice = defaultSalePrice;
        Status = ProductStatus.Active;
    }

    public Guid Id { get; }

    public Sku Sku { get; }

    public string Name { get; }

    public string? Description { get; }

    public Guid CategoryId { get; }

    public string UnitOfMeasure { get; }

    public decimal DefaultSalePrice { get; }

    public ProductStatus Status { get; private set; }

    public void Activate() => Status = ProductStatus.Active;

    public void Deactivate() => Status = ProductStatus.Inactive;
}
