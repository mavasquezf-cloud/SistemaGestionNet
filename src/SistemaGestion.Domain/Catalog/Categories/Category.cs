namespace SistemaGestion.Domain.Catalog.Categories;

public sealed class Category
{
    public Category(Guid id, string name, string? description = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Category ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = true;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string? Description { get; }

    public bool IsActive { get; private set; }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
