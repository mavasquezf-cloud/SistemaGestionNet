using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Application.Purchasing.ReceivePurchase;
using SistemaGestion.Domain.Catalog.Categories;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;
using SistemaGestion.Domain.Purchasing;
using SistemaGestion.Domain.Suppliers;
using SistemaGestion.Infrastructure.Persistence;

namespace SistemaGestion.Infrastructure.IntegrationTests;

public sealed class PurchasingPersistenceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Adding_first_line_to_reloaded_purchase_updates_total_and_rowversion()
    {
        Guid purchaseId;
        Guid productId;
        byte[] originalRowVersion;

        await using (var createScope = fixture.CreateScope())
        {
            var context = createScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
            var repository = createScope.ServiceProvider.GetRequiredService<IPurchaseRepository>();
            var (supplier, product) = await AddDependencies(context);
            var purchase = new Purchase(
                Guid.NewGuid(), new($"PUR-{Guid.NewGuid():N}"[..12]), supplier.Id,
                supplier.Name, DateTimeOffset.UtcNow);
            await repository.AddAsync(purchase);
            await context.SaveChangesAsync();

            purchaseId = purchase.Id;
            productId = product.Id;
            originalRowVersion = context.Entry(purchase).Property<byte[]>("RowVersion").CurrentValue!;
            Assert.NotEmpty(originalRowVersion);
        }

        await using (var updateScope = fixture.CreateScope())
        {
            var context = updateScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
            var repository = updateScope.ServiceProvider.GetRequiredService<IPurchaseRepository>();
            var product = await context.Products.SingleAsync(item => item.Id == productId);
            var purchase = await repository.GetByIdAsync(purchaseId);

            Assert.NotNull(purchase);
            var entry = context.Entry(purchase);
            Assert.Equal(EntityState.Unchanged, entry.State);
            Assert.Equal(originalRowVersion, entry.Property<byte[]>("RowVersion").OriginalValue);
            Assert.Equal(originalRowVersion, entry.Property<byte[]>("RowVersion").CurrentValue);

            purchase.AddLine(
                Guid.NewGuid(), product.Id, product.Name, product.UnitOfMeasure,
                2.1256m, 3.4567m, DateTimeOffset.UtcNow);

            context.ChangeTracker.DetectChanges();
            Assert.Equal(EntityState.Modified, entry.State);
            Assert.Equal(EntityState.Added, context.Entry(purchase.Lines.Single()).State);

            await context.SaveChangesAsync();
            Assert.False(originalRowVersion.SequenceEqual(
                entry.Property<byte[]>("RowVersion").CurrentValue!));
        }

        await using (var verifyScope = fixture.CreateScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
            var persisted = await context.Purchases.Include(item => item.Lines)
                .SingleAsync(item => item.Id == purchaseId);
            Assert.Equal(7.3476m, persisted.Total);
            Assert.Equal(7.3476m, Assert.Single(persisted.Lines).LineTotal);
        }
    }

    [Fact]
    public async Task Purchase_round_trips_complete_aggregate_snapshots_decimals_and_totals()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IPurchaseRepository>();
        var (supplier, product) = await AddDependencies(context);
        var now = DateTimeOffset.UtcNow;
        var purchase = new Purchase(Guid.NewGuid(), new("pur-roundtrip"), supplier.Id, supplier.Name, now, "invoice-42");
        purchase.AddLine(Guid.NewGuid(), product.Id, product.Name, product.UnitOfMeasure, 2.1256m, 3.4567m, now);
        await repository.AddAsync(purchase); await context.SaveChangesAsync(); context.ChangeTracker.Clear();

        var persisted = await repository.GetByIdAsync(purchase.Id);
        Assert.NotNull(persisted); Assert.Equal("PUR-ROUNDTRIP", persisted.PurchaseNumber.Value);
        Assert.Equal("invoice-42", persisted.SupplierDocumentReference); Assert.Equal(supplier.Name, persisted.SupplierName);
        var line = Assert.Single(persisted.Lines); Assert.Equal(product.Name, line.ProductName);
        Assert.Equal(product.UnitOfMeasure, line.UnitOfMeasure); Assert.Equal(7.3476m, line.LineTotal);
        Assert.Equal(line.LineTotal, persisted.Total);
        Assert.Equal(EntityState.Unchanged, context.Entry(persisted).State);
    }

    [Fact]
    public async Task Sequence_generates_unique_formatted_purchase_numbers()
    {
        await using var scope = fixture.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<IPurchaseNumberGenerator>();
        var first = await generator.NextAsync(); var second = await generator.NextAsync();
        Assert.Matches("^PUR-[0-9]{8}$", first.Value); Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Inventory_batch_returns_tracked_matches_and_omits_missing_ids()
    {
        await using var scope = fixture.CreateScope(); var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var (_, firstProduct) = await AddDependencies(context); var category = context.Categories.Local.Single();
        var secondProduct = new Product(Guid.NewGuid(), new($"SKU-{Guid.NewGuid():N}"), "Second", category.Id, "unit", 1);
        context.Products.Add(secondProduct); var first = new InventoryItem(Guid.NewGuid(), firstProduct.Id); var second = new InventoryItem(Guid.NewGuid(), secondProduct.Id);
        context.InventoryItems.AddRange(first, second); await context.SaveChangesAsync(); context.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();
        var result = await repository.GetByProductIdsAsync([firstProduct.Id, secondProduct.Id, Guid.NewGuid()]);
        Assert.Equal(2, result.Count); Assert.All(result.Values, item => Assert.Equal(EntityState.Unchanged, context.Entry(item).State));
    }

    [Fact]
    public async Task Receipt_persists_purchase_inventory_and_movements_in_one_save()
    {
        await using var scope = fixture.CreateScope(); var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var purchases = scope.ServiceProvider.GetRequiredService<IPurchaseRepository>(); var (supplier, product) = await AddDependencies(context);
        var purchase = new Purchase(Guid.NewGuid(), new($"PUR-{Guid.NewGuid():N}"[..12]), supplier.Id, supplier.Name, DateTimeOffset.UtcNow);
        purchase.AddLine(Guid.NewGuid(), product.Id, product.Name, product.UnitOfMeasure, 4, 2, DateTimeOffset.UtcNow); purchase.Confirm(DateTimeOffset.UtcNow);
        await purchases.AddAsync(purchase); await context.SaveChangesAsync(); context.ChangeTracker.Clear();
        var useCase = new ReceivePurchaseUseCase(purchases, scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>(), scope.ServiceProvider.GetRequiredService<IInventoryMovementRepository>(), context, scope.ServiceProvider.GetRequiredService<IClock>());
        var result = await useCase.ExecuteAsync(purchase.Id); Assert.Equal(ReceivePurchaseOutcome.Success, result.Outcome); context.ChangeTracker.Clear();
        var persisted = await context.Purchases.SingleAsync(x => x.Id == purchase.Id); var item = await context.InventoryItems.SingleAsync(x => x.ProductId == product.Id);
        var movement = await context.InventoryMovements.SingleAsync(x => x.Reference == purchase.PurchaseNumber.Value);
        Assert.Equal(PurchaseStatus.Received, persisted.Status); Assert.NotNull(persisted.ReceivedAt); Assert.Equal(4, item.QuantityOnHand);
        Assert.Equal(MovementSource.PurchaseReceipt, movement.Source); Assert.Equal(purchase.PurchaseNumber.Value, movement.Reference);
    }

    private static async Task<(Supplier Supplier, Product Product)> AddDependencies(SistemaGestionDbContext context)
    {
        var category = new Category(Guid.NewGuid(), $"Category-{Guid.NewGuid():N}");
        var supplier = new Supplier(Guid.NewGuid(), new($"SUP-{Guid.NewGuid():N}"), "Supplier snapshot", DateTimeOffset.UtcNow);
        var product = new Product(Guid.NewGuid(), new($"SKU-{Guid.NewGuid():N}"), "Product snapshot", category.Id, "box", 1);
        context.AddRange(category, supplier, product); await context.SaveChangesAsync(); return (supplier, product);
    }
}
