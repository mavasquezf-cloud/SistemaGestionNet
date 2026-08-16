using SistemaGestion.Application.Inventory.GetInventoryByProductId;
using SistemaGestion.Application.Inventory.GetInventoryMovements;
using SistemaGestion.Application.UnitTests.Inventory.Fakes;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Application.UnitTests.Inventory;

public sealed class InventoryQueryUseCaseTests
{
    [Fact]
    public async Task GetInventory_WithProductWithoutItem_ReturnsZero()
    {
        var context = CreateContext();
        var useCase = new GetInventoryByProductIdUseCase(
            context.ProductRepository, context.ItemRepository);

        var result = await useCase.ExecuteAsync(context.Product.Id);

        Assert.Equal(GetInventoryByProductIdOutcome.Found, result.Outcome);
        Assert.Equal(context.Product.Id, result.ProductId);
        Assert.Equal(0m, result.QuantityOnHand);
    }

    [Fact]
    public async Task GetInventory_WithExistingItem_ReturnsCurrentBalanceForInactiveProduct()
    {
        var context = CreateContext();
        context.Product.Deactivate();
        var item = new InventoryItem(Guid.NewGuid(), context.Product.Id);
        item.ApplyManualAdjustment(Guid.NewGuid(), 6.25m, "Opening", null, DateTimeOffset.UtcNow);
        context.ItemRepository.Items.Add(item);
        var useCase = new GetInventoryByProductIdUseCase(
            context.ProductRepository, context.ItemRepository);

        var result = await useCase.ExecuteAsync(context.Product.Id);

        Assert.Equal(GetInventoryByProductIdOutcome.Found, result.Outcome);
        Assert.Equal(6.25m, result.QuantityOnHand);
    }

    [Fact]
    public async Task GetInventory_WithMissingProduct_ReturnsProductNotFound()
    {
        var context = CreateContext();
        var useCase = new GetInventoryByProductIdUseCase(
            context.ProductRepository, context.ItemRepository);
        var missingId = Guid.NewGuid();

        var result = await useCase.ExecuteAsync(missingId);

        Assert.Equal(GetInventoryByProductIdOutcome.ProductNotFound, result.Outcome);
        Assert.Equal(missingId, result.ProductId);
        Assert.Null(result.QuantityOnHand);
    }

    [Fact]
    public async Task GetMovements_WithInvalidPagination_UsesDefaultsAndReturnsNewestFirst()
    {
        var context = CreateContext();
        AddMovements(context, count: 3);
        var useCase = new GetInventoryMovementsUseCase(
            context.ProductRepository, context.MovementRepository);

        var result = await useCase.ExecuteAsync(
            new GetInventoryMovementsQuery(context.Product.Id, 0, 0));

        Assert.Equal(GetInventoryMovementsOutcome.Success, result.Outcome);
        Assert.Equal(1, result.Page);
        Assert.Equal(50, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, context.MovementRepository.RequestedPage);
        Assert.Equal(50, context.MovementRepository.RequestedPageSize);
        Assert.Equal(
            result.Items.OrderByDescending(item => item.OccurredAt).Select(item => item.Id),
            result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetMovements_WithPageSizeAboveMaximum_CapsAtOneHundred()
    {
        var context = CreateContext();
        var useCase = new GetInventoryMovementsUseCase(
            context.ProductRepository, context.MovementRepository);

        var result = await useCase.ExecuteAsync(
            new GetInventoryMovementsQuery(context.Product.Id, 2, 500));

        Assert.Equal(GetInventoryMovementsOutcome.Success, result.Outcome);
        Assert.Equal(2, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(100, context.MovementRepository.RequestedPageSize);
    }

    [Fact]
    public async Task GetMovements_WithMissingProduct_ReturnsProductNotFoundWithoutQueryingHistory()
    {
        var context = CreateContext();
        var useCase = new GetInventoryMovementsUseCase(
            context.ProductRepository, context.MovementRepository);

        var result = await useCase.ExecuteAsync(
            new GetInventoryMovementsQuery(Guid.NewGuid()));

        Assert.Equal(GetInventoryMovementsOutcome.ProductNotFound, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Null(context.MovementRepository.RequestedPage);
    }

    private static TestContext CreateContext()
    {
        var product = new Product(
            Guid.NewGuid(), new Sku($"SKU-{Guid.NewGuid():N}"), "Product",
            Guid.NewGuid(), "unit", 1m);
        var productRepository = new FakeProductRepository();
        productRepository.Products.Add(product);
        return new TestContext(
            product,
            productRepository,
            new FakeInventoryItemRepository(),
            new FakeInventoryMovementRepository());
    }

    private static void AddMovements(TestContext context, int count)
    {
        var item = new InventoryItem(Guid.NewGuid(), context.Product.Id);
        for (var index = 0; index < count; index++)
        {
            var movement = item.ApplyManualAdjustment(
                Guid.NewGuid(), 1m, $"Count {index}", null,
                new DateTimeOffset(2026, 8, 16, 10, index, 0, TimeSpan.Zero));
            context.MovementRepository.Movements.Add(movement);
        }
    }

    private sealed record TestContext(
        Product Product,
        FakeProductRepository ProductRepository,
        FakeInventoryItemRepository ItemRepository,
        FakeInventoryMovementRepository MovementRepository);
}
