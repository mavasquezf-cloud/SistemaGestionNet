using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Application.Catalog.Products.CreateProduct;

public sealed class CreateProductUseCase(
    ICategoryRepository categoryRepository,
    IProductRepository productRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateProductResult> ExecuteAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await categoryRepository.ExistsAsync(command.CategoryId, cancellationToken))
        {
            return new CreateProductResult(CreateProductOutcome.CategoryNotFound, null);
        }

        var sku = new Sku(command.Sku);

        if (await productRepository.ExistsBySkuAsync(sku, cancellationToken))
        {
            return new CreateProductResult(CreateProductOutcome.DuplicateSku, null);
        }

        var product = new Product(
            Guid.NewGuid(),
            sku,
            command.Name,
            command.CategoryId,
            command.UnitOfMeasure,
            command.DefaultSalePrice,
            command.Description);

        await productRepository.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var result = new ProductResult(
            product.Id,
            product.Sku.Value,
            product.Name,
            product.Description,
            product.CategoryId,
            product.UnitOfMeasure,
            product.DefaultSalePrice,
            product.Status);

        return new CreateProductResult(CreateProductOutcome.Success, result);
    }
}
