using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Inventory.Persistence;

namespace SistemaGestion.Application.Inventory.GetInventoryByProductId;

public sealed class GetInventoryByProductIdUseCase(
    IProductRepository productRepository,
    IInventoryItemRepository inventoryItemRepository)
{
    public async Task<GetInventoryByProductIdResult> ExecuteAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var product = await productRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return new GetInventoryByProductIdResult(
                GetInventoryByProductIdOutcome.ProductNotFound, productId, null);
        }

        var inventoryItem = await inventoryItemRepository.GetByProductIdAsync(
            productId, cancellationToken);

        return new GetInventoryByProductIdResult(
            GetInventoryByProductIdOutcome.Found,
            productId,
            inventoryItem?.QuantityOnHand ?? 0m);
    }
}
