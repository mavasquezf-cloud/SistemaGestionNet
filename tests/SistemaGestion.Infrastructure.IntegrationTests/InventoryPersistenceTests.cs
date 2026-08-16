using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Domain.Catalog.Categories;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;
using SistemaGestion.Infrastructure.Persistence;

namespace SistemaGestion.Infrastructure.IntegrationTests;

public sealed class InventoryPersistenceTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture fixture;

    public InventoryPersistenceTests(SqlServerFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task InventoryItem_round_trips_decimal_quantity_and_database_rowversion()
    {
        var product = await CreateProductAsync("ITEM-ROUNDTRIP");
        Guid itemId;
        byte[] initialRowVersion;

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
            var items = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();
            var movements = scope.ServiceProvider.GetRequiredService<IInventoryMovementRepository>();
            var item = new InventoryItem(Guid.NewGuid(), product.Id);
            var movement = item.ApplyManualAdjustment(
                Guid.NewGuid(), 1.23456m, "Precision test", null, DateTimeOffset.UtcNow);
            itemId = item.Id;
            await items.AddAsync(item);
            await movements.AddAsync(movement);
            await context.SaveChangesAsync();
            initialRowVersion = item.RowVersion.ToArray();
        }

        await using var verificationScope = fixture.CreateScope();
        var verificationRepository = verificationScope.ServiceProvider
            .GetRequiredService<IInventoryItemRepository>();
        var persisted = await verificationRepository.GetByProductIdAsync(product.Id);

        Assert.NotNull(persisted);
        Assert.Equal(itemId, persisted.Id);
        Assert.Equal(1.2346m, persisted.QuantityOnHand);
        Assert.NotEmpty(persisted.RowVersion);
        Assert.Equal(initialRowVersion, persisted.RowVersion);
    }

    [Fact]
    public async Task Only_one_InventoryItem_per_Product_is_allowed_and_race_is_translated()
    {
        var product = await CreateProductAsync("ITEM-UNIQUE");
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.InventoryItems.Add(new InventoryItem(Guid.NewGuid(), product.Id));
        context.InventoryItems.Add(new InventoryItem(Guid.NewGuid(), product.Id));

        await Assert.ThrowsAsync<InventoryConcurrencyException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task InventoryItem_Product_foreign_key_is_enforced()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.InventoryItems.Add(new InventoryItem(Guid.NewGuid(), Guid.NewGuid()));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task InventoryMovement_positive_values_round_trip()
    {
        var product = await CreateProductAsync("MOVEMENT-POSITIVE");
        var occurredAt = new DateTimeOffset(2026, 8, 16, 13, 15, 0, TimeSpan.Zero);
        var (item, movement) = CreateAdjustment(product.Id, 5.1256m, occurredAt, "COUNT-1");
        await PersistAsync(item, movement);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryMovementRepository>();
        var page = await repository.GetPageByProductIdAsync(product.Id, 1, 10);
        var persisted = Assert.Single(page.Items);

        Assert.Equal(movement.Id, persisted.Id);
        Assert.Equal(item.Id, persisted.InventoryItemId);
        Assert.Equal(product.Id, persisted.ProductId);
        Assert.Equal(5.1256m, persisted.QuantityDelta);
        Assert.Equal(5.1256m, persisted.ResultingBalance);
        Assert.Equal(InventoryMovementType.Increase, persisted.Type);
        Assert.Equal(MovementSource.ManualAdjustment, persisted.Source);
        Assert.Equal("COUNT-1", persisted.Reference);
        Assert.Equal("Integration test", persisted.Reason);
        Assert.Equal(occurredAt, persisted.OccurredAt);
    }

    [Fact]
    public async Task InventoryMovement_negative_quantity_round_trips()
    {
        var product = await CreateProductAsync("MOVEMENT-NEGATIVE");
        var item = new InventoryItem(Guid.NewGuid(), product.Id);
        var opening = item.ApplyManualAdjustment(
            Guid.NewGuid(), 10m, "Opening", null, DateTimeOffset.UtcNow.AddMinutes(-1));
        var decrease = item.ApplyManualAdjustment(
            Guid.NewGuid(), -2.3456m, "Damage", "DAMAGE-1", DateTimeOffset.UtcNow);
        await PersistAsync(item, opening, decrease);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryMovementRepository>();
        var page = await repository.GetPageByProductIdAsync(product.Id, 1, 10);
        var persisted = Assert.Single(page.Items, movement => movement.Id == decrease.Id);

        Assert.Equal(-2.3456m, persisted.QuantityDelta);
        Assert.Equal(7.6544m, persisted.ResultingBalance);
        Assert.Equal(InventoryMovementType.Decrease, persisted.Type);
    }

    [Fact]
    public async Task InventoryMovement_foreign_keys_are_enforced()
    {
        var product = await CreateProductAsync("MOVEMENT-FK");
        var (item, _) = CreateAdjustment(product.Id, 1m, DateTimeOffset.UtcNow);
        await PersistAsync(item);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();

        await Assert.ThrowsAsync<SqlException>(() => InsertMovementAsync(
            context, Guid.NewGuid(), Guid.NewGuid(), product.Id, 1m, 1m));
        await Assert.ThrowsAsync<SqlException>(() => InsertMovementAsync(
            context, Guid.NewGuid(), item.Id, Guid.NewGuid(), 1m, 1m));
    }

    [Fact]
    public async Task Inventory_database_check_constraints_reject_invalid_balances_and_delta()
    {
        var product = await CreateProductAsync("INVENTORY-CHECKS");
        var productWithoutInventory = await CreateProductAsync("INVENTORY-NEGATIVE-CHECK");
        var (item, _) = CreateAdjustment(product.Id, 1m, DateTimeOffset.UtcNow);
        await PersistAsync(item);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();

        var negativeQuantity = await Assert.ThrowsAsync<SqlException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InventoryItems (Id, ProductId, QuantityOnHand) VALUES ({Guid.NewGuid()}, {productWithoutInventory.Id}, {-1m})"));
        var zeroDelta = await Assert.ThrowsAsync<SqlException>(() => InsertMovementAsync(
            context, Guid.NewGuid(), item.Id, product.Id, 0m, 1m));
        var negativeBalance = await Assert.ThrowsAsync<SqlException>(() => InsertMovementAsync(
            context, Guid.NewGuid(), item.Id, product.Id, 1m, -1m));

        Assert.Contains("CK_InventoryItems_QuantityOnHand_NonNegative", negativeQuantity.Message);
        Assert.Contains("CK_InventoryMovements_QuantityDelta_NonZero", zeroDelta.Message);
        Assert.Contains("CK_InventoryMovements_ResultingBalance_NonNegative", negativeBalance.Message);
    }

    [Fact]
    public async Task Movement_history_is_newest_first_and_paged_in_SQL()
    {
        var product = await CreateProductAsync("MOVEMENT-PAGING");
        var item = new InventoryItem(Guid.NewGuid(), product.Id);
        var oldest = item.ApplyManualAdjustment(
            Guid.NewGuid(), 1m, "Oldest", null, new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero));
        var middle = item.ApplyManualAdjustment(
            Guid.NewGuid(), 1m, "Middle", null, new DateTimeOffset(2026, 8, 16, 11, 0, 0, TimeSpan.Zero));
        var newest = item.ApplyManualAdjustment(
            Guid.NewGuid(), 1m, "Newest", null, new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));
        await PersistAsync(item, oldest, middle, newest);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryMovementRepository>();
        var firstPage = await repository.GetPageByProductIdAsync(product.Id, 1, 2);
        var secondPage = await repository.GetPageByProductIdAsync(product.Id, 2, 2);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal([newest.Id, middle.Id], firstPage.Items.Select(movement => movement.Id));
        Assert.Equal(oldest.Id, Assert.Single(secondPage.Items).Id);
    }

    [Fact]
    public async Task RowVersion_changes_after_balance_update()
    {
        var product = await CreateProductAsync("ROWVERSION-CHANGE");
        var (item, opening) = CreateAdjustment(product.Id, 1m, DateTimeOffset.UtcNow);
        await PersistAsync(item, opening);
        var originalRowVersion = item.RowVersion.ToArray();

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var items = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();
        var movements = scope.ServiceProvider.GetRequiredService<IInventoryMovementRepository>();
        var tracked = await items.GetByProductIdAsync(product.Id);
        var movement = tracked!.ApplyManualAdjustment(
            Guid.NewGuid(), 1m, "Second", null, DateTimeOffset.UtcNow);
        await movements.AddAsync(movement);
        await context.SaveChangesAsync();

        Assert.NotEqual(originalRowVersion, tracked.RowVersion);
        Assert.NotEmpty(tracked.RowVersion);
    }

    [Fact]
    public async Task Concurrent_losing_adjustment_is_translated_and_rolled_back_atomically()
    {
        var product = await CreateProductAsync("ROWVERSION-CONFLICT");
        var (item, opening) = CreateAdjustment(product.Id, 10m, DateTimeOffset.UtcNow.AddMinutes(-1));
        await PersistAsync(item, opening);

        await using var winnerScope = fixture.CreateScope();
        await using var loserScope = fixture.CreateScope();
        var winnerContext = winnerScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var loserContext = loserScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var winnerItems = winnerScope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();
        var loserItems = loserScope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();
        var winnerMovements = winnerScope.ServiceProvider.GetRequiredService<IInventoryMovementRepository>();
        var loserMovements = loserScope.ServiceProvider.GetRequiredService<IInventoryMovementRepository>();
        var winnerItem = await winnerItems.GetByProductIdAsync(product.Id);
        var loserItem = await loserItems.GetByProductIdAsync(product.Id);
        var winnerMovement = winnerItem!.ApplyManualAdjustment(
            Guid.NewGuid(), 2m, "Winner", null, DateTimeOffset.UtcNow);
        var loserMovement = loserItem!.ApplyManualAdjustment(
            Guid.NewGuid(), 3m, "Loser", null, DateTimeOffset.UtcNow.AddSeconds(1));
        await winnerMovements.AddAsync(winnerMovement);
        await loserMovements.AddAsync(loserMovement);

        await winnerContext.SaveChangesAsync();
        await Assert.ThrowsAsync<InventoryConcurrencyException>(() => loserContext.SaveChangesAsync());

        await using var verificationScope = fixture.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<SistemaGestionDbContext>();
        var persistedItem = await verificationContext.InventoryItems
            .AsNoTracking()
            .SingleAsync(current => current.ProductId == product.Id);
        var persistedMovementIds = await verificationContext.InventoryMovements
            .AsNoTracking()
            .Where(movement => movement.ProductId == product.Id)
            .Select(movement => movement.Id)
            .ToListAsync();

        Assert.Equal(12m, persistedItem.QuantityOnHand);
        Assert.Contains(winnerMovement.Id, persistedMovementIds);
        Assert.DoesNotContain(loserMovement.Id, persistedMovementIds);
        Assert.Equal(2, persistedMovementIds.Count);
    }

    private async Task<Product> CreateProductAsync(string skuPrefix)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var category = new Category(Guid.NewGuid(), $"Category {Guid.NewGuid():N}");
        var product = new Product(
            Guid.NewGuid(), new Sku($"{skuPrefix}-{Guid.NewGuid():N}"), "Inventory product",
            category.Id, "unit", 1m);
        context.Categories.Add(category);
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private async Task PersistAsync(InventoryItem item, params InventoryMovement[] movements)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.InventoryItems.Add(item);
        context.InventoryMovements.AddRange(movements);
        await context.SaveChangesAsync();
    }

    private static (InventoryItem Item, InventoryMovement Movement) CreateAdjustment(
        Guid productId,
        decimal quantityDelta,
        DateTimeOffset occurredAt,
        string? reference = null)
    {
        var item = new InventoryItem(Guid.NewGuid(), productId);
        var movement = item.ApplyManualAdjustment(
            Guid.NewGuid(), quantityDelta, "Integration test", reference, occurredAt);
        return (item, movement);
    }

    private static Task<int> InsertMovementAsync(
        SistemaGestionDbContext context,
        Guid id,
        Guid inventoryItemId,
        Guid productId,
        decimal quantityDelta,
        decimal resultingBalance)
    {
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO InventoryMovements
                (Id, InventoryItemId, ProductId, QuantityDelta, ResultingBalance, Type, Source, Reference, Reason, OccurredAt)
            VALUES
                ({id}, {inventoryItemId}, {productId}, {quantityDelta}, {resultingBalance},
                 {"Increase"}, {"ManualAdjustment"}, {null}, {"Constraint test"}, {DateTimeOffset.UtcNow})
            """);
    }
}
