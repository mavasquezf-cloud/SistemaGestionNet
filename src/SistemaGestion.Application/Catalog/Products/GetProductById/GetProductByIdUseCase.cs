using SistemaGestion.Application.Catalog.Persistence;

namespace SistemaGestion.Application.Catalog.Products.GetProductById;

public sealed class GetProductByIdUseCase(IProductRepository productRepository)
{
    public async Task<GetProductByIdResult> ExecuteAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var product = await productRepository.GetByIdAsync(productId, cancellationToken);

        return product is null
            ? new GetProductByIdResult(false, null)
            : new GetProductByIdResult(true, product.ToResult());
    }
}
