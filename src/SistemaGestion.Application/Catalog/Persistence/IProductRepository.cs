using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Application.Catalog.Persistence;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    Task<ProductWithCategory?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<bool> ExistsBySkuAsync(Sku sku, CancellationToken cancellationToken = default);
}
