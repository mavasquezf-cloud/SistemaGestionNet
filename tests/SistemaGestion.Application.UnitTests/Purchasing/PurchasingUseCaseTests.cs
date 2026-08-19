using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Application.Purchasing;
using SistemaGestion.Application.Purchasing.AddPurchaseLine;
using SistemaGestion.Application.Purchasing.CancelPurchase;
using SistemaGestion.Application.Purchasing.ConfirmPurchase;
using SistemaGestion.Application.Purchasing.CreatePurchase;
using SistemaGestion.Application.Purchasing.GetPurchaseById;
using SistemaGestion.Application.Purchasing.GetPurchases;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Application.Purchasing.ReceivePurchase;
using SistemaGestion.Application.Suppliers.Persistence;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;
using SistemaGestion.Domain.Purchasing;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.UnitTests.Purchasing;

public sealed class PurchasingUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_rejects_missing_and_inactive_supplier_without_saving()
    {
        var f = new Fixture();
        var missing = await f.Create().ExecuteAsync(new(Guid.NewGuid()));
        var supplier = f.AddSupplier("Snapshot supplier"); supplier.Deactivate(Now);
        var inactive = await f.Create().ExecuteAsync(new(supplier.Id));
        Assert.Equal(CreatePurchaseOutcome.SupplierNotFound, missing.Outcome);
        Assert.Equal(CreatePurchaseOutcome.SupplierInactive, inactive.Outcome);
        Assert.Equal(0, f.Uow.Calls);
    }

    [Fact]
    public async Task Create_snapshots_supplier_generated_number_and_clock_and_saves_once()
    {
        var f = new Fixture(); var supplier = f.AddSupplier(" Snapshot supplier ");
        var result = await f.Create().ExecuteAsync(new(supplier.Id, " INV-1 "));
        Assert.Equal(CreatePurchaseOutcome.Success, result.Outcome);
        Assert.Equal("PO-0001", result.Purchase!.PurchaseNumber);
        Assert.Equal("Snapshot supplier", result.Purchase.SupplierName);
        Assert.Equal(Now, result.Purchase.CreatedAt);
        Assert.Equal(PurchaseStatus.Draft, result.Purchase.Status);
        Assert.Equal(1, f.Uow.Calls);
    }

    [Fact]
    public async Task Create_maps_duplicate_number_race()
    {
        var f = new Fixture(); var supplier = f.AddSupplier(); f.Uow.Exception = new PurchaseDuplicateNumberException("duplicate");
        var result = await f.Create().ExecuteAsync(new(supplier.Id));
        Assert.Equal(CreatePurchaseOutcome.DuplicatePurchaseNumber, result.Outcome);
        Assert.Equal(1, f.Uow.Calls);
    }

    [Fact]
    public async Task Add_line_maps_rejections_snapshots_product_and_preserves_domain_validation()
    {
        var f = new Fixture(); var purchase = f.AddPurchase(); var product = f.AddProduct("Widget", "box");
        Assert.Equal(AddPurchaseLineOutcome.ProductNotFound, (await f.AddLine().ExecuteAsync(new(purchase.Id, Guid.NewGuid(), 1, 2))).Outcome);
        product.Deactivate();
        Assert.Equal(AddPurchaseLineOutcome.ProductInactive, (await f.AddLine().ExecuteAsync(new(purchase.Id, product.Id, 1, 2))).Outcome);
        product.Activate();
        var success = await f.AddLine().ExecuteAsync(new(purchase.Id, product.Id, 2, 3));
        Assert.Equal("Widget", success.Purchase!.Lines.Single().ProductName);
        Assert.Equal("box", success.Purchase.Lines.Single().UnitOfMeasure);
        Assert.Equal(6, success.Purchase.Total);
        Assert.Equal(AddPurchaseLineOutcome.DuplicateProduct, (await f.AddLine().ExecuteAsync(new(purchase.Id, product.Id, 1, 1))).Outcome);
        var second = f.AddProduct();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.AddLine().ExecuteAsync(new(purchase.Id, second.Id, 0, 1)));
        Assert.Equal(1, f.Uow.Calls);
    }

    [Fact]
    public async Task Confirm_handles_missing_empty_success_invalid_and_concurrency()
    {
        var f = new Fixture();
        Assert.Equal(ConfirmPurchaseOutcome.PurchaseNotFound, (await f.Confirm().ExecuteAsync(Guid.NewGuid())).Outcome);
        var empty = f.AddPurchase(); Assert.Equal(ConfirmPurchaseOutcome.EmptyPurchase, (await f.Confirm().ExecuteAsync(empty.Id)).Outcome);
        var ready = f.AddPurchaseWithLine(); var success = await f.Confirm().ExecuteAsync(ready.Id);
        Assert.Equal(PurchaseStatus.Confirmed, success.Purchase!.Status); Assert.Equal(Now, success.Purchase.UpdatedAt);
        Assert.Equal(ConfirmPurchaseOutcome.InvalidStatus, (await f.Confirm().ExecuteAsync(ready.Id)).Outcome);
        var concurrent = f.AddPurchaseWithLine(); f.Uow.Exception = new PurchaseConcurrencyException("race");
        Assert.Equal(ConfirmPurchaseOutcome.ConcurrencyConflict, (await f.Confirm().ExecuteAsync(concurrent.Id)).Outcome);
    }

    [Fact]
    public async Task Cancel_accepts_draft_and_confirmed_but_not_received_and_never_adds_movements()
    {
        var f = new Fixture(); var draft = f.AddPurchase();
        Assert.Equal(CancelPurchaseOutcome.Success, (await f.Cancel().ExecuteAsync(draft.Id)).Outcome);
        var confirmed = f.AddConfirmedPurchase();
        Assert.Equal(CancelPurchaseOutcome.Success, (await f.Cancel().ExecuteAsync(confirmed.Id)).Outcome);
        var received = f.AddConfirmedPurchase(); received.Receive(Now);
        Assert.Equal(CancelPurchaseOutcome.InvalidStatus, (await f.Cancel().ExecuteAsync(received.Id)).Outcome);
        Assert.Empty(f.Movements.Items); Assert.Equal(2, f.Uow.Calls);
    }

    [Fact]
    public async Task Receive_rejects_missing_draft_and_already_received_without_saving()
    {
        var f = new Fixture();
        Assert.Equal(ReceivePurchaseOutcome.PurchaseNotFound, (await f.Receive().ExecuteAsync(Guid.NewGuid())).Outcome);
        Assert.Equal(ReceivePurchaseOutcome.PurchaseNotConfirmed, (await f.Receive().ExecuteAsync(f.AddPurchase().Id)).Outcome);
        var received = f.AddConfirmedPurchase(); received.Receive(Now);
        Assert.Equal(ReceivePurchaseOutcome.AlreadyReceived, (await f.Receive().ExecuteAsync(received.Id)).Outcome);
        Assert.Equal(0, f.Uow.Calls);
    }

    [Fact]
    public async Task Receive_batches_items_creates_missing_movements_and_uses_one_timestamp_and_save()
    {
        var f = new Fixture(); var purchase = f.AddPurchase(); var p1 = f.AddProduct("One"); var p2 = f.AddProduct("Two");
        purchase.AddLine(Guid.NewGuid(), p1.Id, p1.Name, p1.UnitOfMeasure, 2, 4, Now);
        purchase.AddLine(Guid.NewGuid(), p2.Id, p2.Name, p2.UnitOfMeasure, 3, 5, Now); purchase.Confirm(Now);
        var existing = new InventoryItem(Guid.NewGuid(), p1.Id); f.Inventory.Items.Add(existing);
        var result = await f.Receive().ExecuteAsync(purchase.Id);
        Assert.Equal(ReceivePurchaseOutcome.Success, result.Outcome); Assert.Equal(PurchaseStatus.Received, purchase.Status);
        Assert.Equal(1, f.Inventory.BatchCalls); Assert.Equal(1, f.Inventory.AddCalls); Assert.Equal(2, f.Movements.Items.Count);
        Assert.All(f.Movements.Items, m => { Assert.Equal(MovementSource.PurchaseReceipt, m.Source); Assert.Equal(InventoryMovementType.Increase, m.Type); Assert.Equal("PO-0001", m.Reference); Assert.Equal(Now, m.OccurredAt); });
        Assert.Equal(2, existing.QuantityOnHand); Assert.Equal(3, f.Inventory.Items.Single(x => x.ProductId == p2.Id).QuantityOnHand);
        Assert.Equal(Now, purchase.ReceivedAt); Assert.Equal(1, f.Uow.Calls); Assert.Equal(2, f.Movements.CountAtFirstSave);
    }

    [Theory]
    [InlineData("purchase")]
    [InlineData("inventory")]
    [InlineData("receipt")]
    public async Task Receive_maps_all_persistence_conflicts(string kind)
    {
        var f = new Fixture(); var purchase = f.AddConfirmedPurchase();
        f.Uow.Exception = kind switch { "purchase" => new PurchaseConcurrencyException("race"), "inventory" => new InventoryConcurrencyException("race"), _ => new PurchaseReceiptConflictException("race") };
        Assert.Equal(ReceivePurchaseOutcome.ConcurrencyConflict, (await f.Receive().ExecuteAsync(purchase.Id)).Outcome);
        Assert.Equal(1, f.Uow.Calls);
    }

    [Fact]
    public async Task Queries_map_lines_and_normalize_pagination()
    {
        var f = new Fixture(); var purchase = f.AddPurchaseWithLine();
        var detail = await new GetPurchaseByIdUseCase(f.Purchases).ExecuteAsync(purchase.Id);
        Assert.Equal(GetPurchaseByIdOutcome.Found, detail.Outcome); Assert.Single(detail.Purchase!.Lines);
        Assert.Equal(GetPurchaseByIdOutcome.NotFound, (await new GetPurchaseByIdUseCase(f.Purchases).ExecuteAsync(Guid.NewGuid())).Outcome);
        var defaults = await new GetPurchasesUseCase(f.Purchases).ExecuteAsync(new(0, 0));
        Assert.Equal(1, defaults.Page); Assert.Equal(20, defaults.PageSize); Assert.Equal(1, defaults.TotalCount);
        var capped = await new GetPurchasesUseCase(f.Purchases).ExecuteAsync(new(2, 500)); Assert.Equal(100, capped.PageSize);
    }

    private sealed class Fixture
    {
        public FakePurchaseRepository Purchases { get; } = new(); public FakeSupplierRepository Suppliers { get; } = new();
        public FakeProductRepository Products { get; } = new(); public FakeInventoryRepository Inventory { get; } = new();
        public FakeMovementRepository Movements { get; } = new(); public FakeUow Uow { get; } public FakeClock Clock { get; } = new(Now);
        public Fixture() { Uow = new(Movements); }
        public Supplier AddSupplier(string name = "Supplier") { var x = new Supplier(Guid.NewGuid(), new("SUP-1"), name, Now); Suppliers.Items.Add(x); return x; }
        public Product AddProduct(string name = "Product", string unit = "unit") { var x = new Product(Guid.NewGuid(), new($"SKU-{Products.Items.Count + 1}"), name, Guid.NewGuid(), unit, 1); Products.Items.Add(x); return x; }
        public Purchase AddPurchase() { var x = new Purchase(Guid.NewGuid(), new("PO-0001"), Guid.NewGuid(), "Supplier", Now); Purchases.Items.Add(x); return x; }
        public Purchase AddPurchaseWithLine() { var x = AddPurchase(); var p = AddProduct(); x.AddLine(Guid.NewGuid(), p.Id, p.Name, p.UnitOfMeasure, 1, 1, Now); return x; }
        public Purchase AddConfirmedPurchase() { var x = AddPurchaseWithLine(); x.Confirm(Now); return x; }
        public CreatePurchaseUseCase Create() => new(Suppliers, Purchases, new FakeNumbers(), Uow, Clock);
        public AddPurchaseLineUseCase AddLine() => new(Purchases, Products, Uow, Clock);
        public ConfirmPurchaseUseCase Confirm() => new(Purchases, Uow, Clock); public CancelPurchaseUseCase Cancel() => new(Purchases, Uow, Clock);
        public ReceivePurchaseUseCase Receive() => new(Purchases, Inventory, Movements, Uow, Clock);
    }

    private sealed class FakePurchaseRepository : IPurchaseRepository
    {
        public List<Purchase> Items { get; } = [];
        public Task AddAsync(Purchase x, CancellationToken c = default) { Items.Add(x); return Task.CompletedTask; }
        public Task<Purchase?> GetByIdAsync(Guid id, CancellationToken c = default) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id));
        public Task<PurchasePage> GetPageAsync(int page, int size, CancellationToken c = default) { var ordered = Items.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToArray(); return Task.FromResult(new PurchasePage(ordered.Skip((page - 1) * size).Take(size).ToArray(), ordered.Length)); }
        public Task<bool> ExistsByPurchaseNumberAsync(PurchaseNumber number, CancellationToken c = default) => Task.FromResult(Items.Any(x => x.PurchaseNumber == number));
    }
    private sealed class FakeSupplierRepository : ISupplierRepository
    {
        public List<Supplier> Items { get; } = []; public Task AddAsync(Supplier x, CancellationToken c = default) => throw new NotSupportedException();
        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken c = default) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id));
        public Task<SupplierPage> GetPageAsync(int p, int s, CancellationToken c = default) => throw new NotSupportedException();
        public Task<bool> ExistsBySupplierNumberAsync(SupplierNumber n, CancellationToken c = default) => throw new NotSupportedException();
    }
    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Items { get; } = []; public Task AddAsync(Product x, CancellationToken c = default) => throw new NotSupportedException();
        public Task<ProductWithCategory?> GetByIdAsync(Guid id, CancellationToken c = default) { var x = Items.SingleOrDefault(y => y.Id == id); return Task.FromResult(x is null ? null : new ProductWithCategory(x, "Category")); }
        public Task<ProductPage> GetPageAsync(int p, int s, CancellationToken c = default) => throw new NotSupportedException(); public Task<bool> ExistsBySkuAsync(Sku s, CancellationToken c = default) => throw new NotSupportedException();
    }
    private sealed class FakeInventoryRepository : IInventoryItemRepository
    {
        public List<InventoryItem> Items { get; } = []; public int AddCalls { get; private set; } public int BatchCalls { get; private set; }
        public Task AddAsync(InventoryItem x, CancellationToken c = default) { AddCalls++; Items.Add(x); return Task.CompletedTask; }
        public Task<InventoryItem?> GetByProductIdAsync(Guid id, CancellationToken c = default) => Task.FromResult(Items.SingleOrDefault(x => x.ProductId == id));
        public Task<IReadOnlyDictionary<Guid, InventoryItem>> GetByProductIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken c = default) { BatchCalls++; return Task.FromResult<IReadOnlyDictionary<Guid, InventoryItem>>(Items.Where(x => ids.Contains(x.ProductId)).ToDictionary(x => x.ProductId)); }
    }
    private sealed class FakeMovementRepository : IInventoryMovementRepository
    {
        public List<InventoryMovement> Items { get; } = []; public int CountAtFirstSave { get; set; }
        public Task AddAsync(InventoryMovement x, CancellationToken c = default) { Items.Add(x); return Task.CompletedTask; }
        public Task<InventoryMovementPage> GetPageByProductIdAsync(Guid id, int p, int s, CancellationToken c = default) => throw new NotSupportedException();
    }
    private sealed class FakeUow(FakeMovementRepository movements) : IUnitOfWork
    {
        public int Calls { get; private set; } public Exception? Exception { get; set; }
        public Task<int> SaveChangesAsync(CancellationToken c = default) { Calls++; if (Calls == 1) movements.CountAtFirstSave = movements.Items.Count; if (Exception is not null) throw Exception; return Task.FromResult(1); }
    }
    private sealed class FakeNumbers : IPurchaseNumberGenerator { public Task<PurchaseNumber> NextAsync(CancellationToken c = default) => Task.FromResult(new PurchaseNumber("PO-0001")); }
    private sealed class FakeClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; } = now; }
}
