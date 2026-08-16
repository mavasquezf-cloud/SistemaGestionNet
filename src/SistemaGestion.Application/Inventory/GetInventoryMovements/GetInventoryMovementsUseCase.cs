using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Inventory.Persistence;

namespace SistemaGestion.Application.Inventory.GetInventoryMovements;

public sealed class GetInventoryMovementsUseCase(
    IProductRepository productRepository,
    IInventoryMovementRepository inventoryMovementRepository)
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private const int MaximumPageSize = 100;

    public async Task<GetInventoryMovementsResult> ExecuteAsync(
        GetInventoryMovementsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page < 1 ? DefaultPage : query.Page;
        var pageSize = query.PageSize < 1
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaximumPageSize);

        var product = await productRepository.GetByIdAsync(query.ProductId, cancellationToken);
        if (product is null)
        {
            return new GetInventoryMovementsResult(
                GetInventoryMovementsOutcome.ProductNotFound,
                [],
                page,
                pageSize,
                0);
        }

        var movementPage = await inventoryMovementRepository.GetPageByProductIdAsync(
            query.ProductId, page, pageSize, cancellationToken);

        return new GetInventoryMovementsResult(
            GetInventoryMovementsOutcome.Success,
            movementPage.Items.Select(InventoryMovementResult.FromMovement).ToArray(),
            page,
            pageSize,
            movementPage.TotalCount);
    }
}
