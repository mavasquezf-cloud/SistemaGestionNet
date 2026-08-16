using SistemaGestion.Application.Inventory.AdjustInventory;
using SistemaGestion.Application.UnitTests.Inventory.Fakes;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Application.UnitTests.Inventory;

public sealed class AdjustInventoryUseCaseTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 8, 16, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Execute_WithMissingProduct_ReturnsProductNotFoundWithoutWriting()
    {
        var context = CreateContext(addProduct: false);

        var result = await context.UseCase.ExecuteAsync(Command(Guid.NewGuid(), 1m));

        Assert.Equal(AdjustInventoryOutcome.ProductNotFound, result.Outcome);
        AssertRejectedWithoutCommit(context);
    }

    [Fact]
    public async Task Execute_WithInactiveProduct_ReturnsProductInactiveWithoutWriting()
    {
        var context = CreateContext();
        context.Product!.Deactivate();

        var result = await context.UseCase.ExecuteAsync(Command(context.Product.Id, 1m));

        Assert.Equal(AdjustInventoryOutcome.ProductInactive, result.Outcome);
        AssertRejectedWithoutCommit(context);
    }

    [Fact]
    public async Task Execute_WithPositiveAdjustment_CreatesItemMovementAndCommitsOnce()
    {
        var context = CreateContext();

        var result = await context.UseCase.ExecuteAsync(
            Command(context.Product!.Id, 8.5m, " Initial inventory ", " COUNT-1 "));

        Assert.Equal(AdjustInventoryOutcome.Success, result.Outcome);
        Assert.Equal(8.5m, result.QuantityOnHand);
        Assert.Equal(1, context.ItemRepository.AddCallCount);
        var item = Assert.Single(context.ItemRepository.Items);
        Assert.Equal(8.5m, item.QuantityOnHand);
        var movement = Assert.Single(context.MovementRepository.Movements);
        Assert.Equal(movement.Id, result.Movement!.Id);
        Assert.Equal(8.5m, result.Movement.QuantityDelta);
        Assert.Equal(8.5m, result.Movement.ResultingBalance);
        Assert.Equal("Initial inventory", result.Movement.Reason);
        Assert.Equal("COUNT-1", result.Movement.Reference);
        Assert.Equal(FixedTime, result.Movement.OccurredAt);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Execute_WithExistingItem_AdjustsItWithoutAddingAnotherItem()
    {
        var context = CreateContext();
        var item = new InventoryItem(Guid.NewGuid(), context.Product!.Id);
        item.ApplyManualAdjustment(Guid.NewGuid(), 4m, "Opening", null, FixedTime.AddDays(-1));
        context.ItemRepository.Items.Add(item);

        var result = await context.UseCase.ExecuteAsync(Command(context.Product.Id, 3m));

        Assert.Equal(AdjustInventoryOutcome.Success, result.Outcome);
        Assert.Equal(7m, result.QuantityOnHand);
        Assert.Equal(0, context.ItemRepository.AddCallCount);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Execute_WithValidNegativeAdjustment_DecreasesExistingBalance()
    {
        var context = CreateContext();
        var item = new InventoryItem(Guid.NewGuid(), context.Product!.Id);
        item.ApplyManualAdjustment(Guid.NewGuid(), 10m, "Opening", null, FixedTime.AddDays(-1));
        context.ItemRepository.Items.Add(item);

        var result = await context.UseCase.ExecuteAsync(Command(context.Product.Id, -2.5m));

        Assert.Equal(AdjustInventoryOutcome.Success, result.Outcome);
        Assert.Equal(7.5m, result.QuantityOnHand);
        Assert.Equal(-2.5m, result.Movement!.QuantityDelta);
        Assert.Equal(InventoryMovementType.Decrease, result.Movement.Type);
    }

    [Fact]
    public async Task Execute_WithInsufficientStock_ReturnsOutcomeWithoutWriting()
    {
        var context = CreateContext();

        var result = await context.UseCase.ExecuteAsync(Command(context.Product!.Id, -1m));

        Assert.Equal(AdjustInventoryOutcome.InsufficientStock, result.Outcome);
        AssertRejectedWithoutCommit(context);
    }

    [Fact]
    public async Task Execute_WhenSaveReportsConcurrency_ReturnsExplicitConflictOutcome()
    {
        var context = CreateContext();
        context.UnitOfWork.ThrowConcurrencyConflict = true;

        var result = await context.UseCase.ExecuteAsync(Command(context.Product!.Id, 1m));

        Assert.Equal(AdjustInventoryOutcome.ConcurrencyConflict, result.Outcome);
        Assert.Null(result.QuantityOnHand);
        Assert.Null(result.Movement);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    private static TestContext CreateContext(bool addProduct = true)
    {
        var productRepository = new FakeProductRepository();
        Product? product = null;
        if (addProduct)
        {
            product = new Product(
                Guid.NewGuid(), new Sku($"SKU-{Guid.NewGuid():N}"), "Product",
                Guid.NewGuid(), "unit", 1m);
            productRepository.Products.Add(product);
        }

        var itemRepository = new FakeInventoryItemRepository();
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new AdjustInventoryUseCase(
            productRepository, itemRepository, movementRepository, unitOfWork, new FakeClock(FixedTime));
        return new TestContext(
            product, itemRepository, movementRepository, unitOfWork, useCase);
    }

    private static AdjustInventoryCommand Command(
        Guid productId,
        decimal quantityDelta,
        string reason = "Inventory count",
        string? reference = null) =>
        new(productId, quantityDelta, reason, reference);

    private static void AssertRejectedWithoutCommit(TestContext context)
    {
        Assert.Empty(context.ItemRepository.Items);
        Assert.Empty(context.MovementRepository.Movements);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCallCount);
    }

    private sealed record TestContext(
        Product? Product,
        FakeInventoryItemRepository ItemRepository,
        FakeInventoryMovementRepository MovementRepository,
        FakeUnitOfWork UnitOfWork,
        AdjustInventoryUseCase UseCase);
}
