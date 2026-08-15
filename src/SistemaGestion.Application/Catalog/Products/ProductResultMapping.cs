using SistemaGestion.Application.Catalog.Persistence;

namespace SistemaGestion.Application.Catalog.Products;

internal static class ProductResultMapping
{
    public static ProductWithCategoryResult ToResult(this ProductWithCategory source)
    {
        var product = source.Product;

        return new ProductWithCategoryResult(
            product.Id,
            product.Sku.Value,
            product.Name,
            product.Description,
            product.CategoryId,
            source.CategoryName,
            product.UnitOfMeasure,
            product.DefaultSalePrice,
            product.Status);
    }
}
