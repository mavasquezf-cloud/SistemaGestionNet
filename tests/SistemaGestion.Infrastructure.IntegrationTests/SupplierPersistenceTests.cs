using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaGestion.Application.Suppliers.Persistence;
using SistemaGestion.Domain.Suppliers;
using SistemaGestion.Infrastructure.Persistence;

namespace SistemaGestion.Infrastructure.IntegrationTests;

public sealed class SupplierPersistenceTests : IClassFixture<SqlServerFixture>
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 18, 9, 30, 0, TimeSpan.Zero);

    private readonly SqlServerFixture fixture;

    public SupplierPersistenceTests(SqlServerFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Supplier_round_trips_normalized_number_optional_fields_status_and_timestamps()
    {
        var supplier = new Supplier(
            Guid.NewGuid(),
            new SupplierNumber("  sup-001  "),
            "Supplier One",
            CreatedAt,
            "TAX-001",
            "sales@example.com",
            "+593 555 0100",
            "Quito, Ecuador");
        await PersistAsync(supplier);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ISupplierRepository>();
        var persisted = await repository.GetByIdAsync(supplier.Id);
        var storedStatus = await context.Database
            .SqlQuery<string>($"SELECT [Status] AS [Value] FROM [Suppliers] WHERE [Id] = {supplier.Id}")
            .SingleAsync();

        Assert.NotNull(persisted);
        Assert.Equal("SUP-001", persisted.SupplierNumber.Value);
        Assert.Equal("Supplier One", persisted.Name);
        Assert.Equal("TAX-001", persisted.TaxIdentificationNumber);
        Assert.Equal("sales@example.com", persisted.Email);
        Assert.Equal("+593 555 0100", persisted.Phone);
        Assert.Equal("Quito, Ecuador", persisted.Address);
        Assert.Equal(SupplierStatus.Active, persisted.Status);
        Assert.Equal("Active", storedStatus);
        Assert.Equal(CreatedAt, persisted.CreatedAt);
        Assert.Equal(CreatedAt, persisted.UpdatedAt);
    }

    [Fact]
    public async Task Optional_supplier_fields_round_trip_as_null()
    {
        var supplier = CreateSupplier("SUP-OPTIONAL");
        await PersistAsync(supplier);

        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISupplierRepository>();
        var persisted = await repository.GetByIdAsync(supplier.Id);

        Assert.NotNull(persisted);
        Assert.Null(persisted.TaxIdentificationNumber);
        Assert.Null(persisted.Email);
        Assert.Null(persisted.Phone);
        Assert.Null(persisted.Address);
    }

    [Fact]
    public async Task Normalized_duplicate_number_is_rejected_and_translated_specifically()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.Suppliers.Add(CreateSupplier("SUP-DUPLICATE"));
        context.Suppliers.Add(CreateSupplier("  sup-duplicate  "));

        await Assert.ThrowsAsync<SupplierDuplicateNumberException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Unrelated_unique_violation_is_not_translated_as_duplicate_supplier_number()
    {
        var supplier = CreateSupplier("SUP-PK-ONE");
        await PersistAsync(supplier);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.Suppliers.Add(new Supplier(
            supplier.Id, new SupplierNumber("SUP-PK-TWO"), "Other", CreatedAt));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.IsNotType<SupplierDuplicateNumberException>(exception);
    }

    [Fact]
    public async Task Supplier_page_is_ordered_paged_untracked_and_includes_all_statuses()
    {
        var first = CreateSupplier("SUP-100");
        var second = CreateSupplier("SUP-200");
        second.Deactivate(CreatedAt.AddHours(1));
        var third = CreateSupplier("SUP-300");
        await PersistAsync(third, first, second);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ISupplierRepository>();
        var firstPage = await repository.GetPageAsync(1, 2);
        var secondPage = await repository.GetPageAsync(2, 2);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal([first.Id, second.Id], firstPage.Items.Select(supplier => supplier.Id));
        Assert.Equal(third.Id, Assert.Single(secondPage.Items).Id);
        Assert.Contains(firstPage.Items, supplier => supplier.Status == SupplierStatus.Active);
        Assert.Contains(firstPage.Items, supplier => supplier.Status == SupplierStatus.Inactive);
        Assert.Empty(context.ChangeTracker.Entries<Supplier>());
    }

    [Fact]
    public async Task GetById_is_tracked_and_rowversion_is_generated_then_changes_on_status_update()
    {
        var supplier = CreateSupplier("SUP-TRACKED");
        byte[] initialRowVersion;
        await using (var creationScope = fixture.CreateScope())
        {
            var context = creationScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            initialRowVersion = context.Entry(supplier)
                .Property<byte[]>("RowVersion").CurrentValue!.ToArray();
        }

        await using var updateScope = fixture.CreateScope();
        var updateContext = updateScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var repository = updateScope.ServiceProvider.GetRequiredService<ISupplierRepository>();
        var tracked = await repository.GetByIdAsync(supplier.Id);
        Assert.NotNull(tracked);
        Assert.Equal(EntityState.Unchanged, updateContext.Entry(tracked).State);
        tracked.Deactivate(CreatedAt.AddHours(1));
        await updateContext.SaveChangesAsync();
        var updatedRowVersion = updateContext.Entry(tracked)
            .Property<byte[]>("RowVersion").CurrentValue;

        Assert.NotEmpty(initialRowVersion);
        Assert.NotNull(updatedRowVersion);
        Assert.NotEqual(initialRowVersion, updatedRowVersion);
        Assert.Equal(SupplierStatus.Inactive, tracked.Status);
    }

    [Fact]
    public async Task Concurrent_status_change_translates_conflict_and_preserves_winner_state()
    {
        var supplier = CreateSupplier("SUP-CONCURRENCY");
        await PersistAsync(supplier);

        await using var winnerScope = fixture.CreateScope();
        await using var loserScope = fixture.CreateScope();
        var winnerContext = winnerScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var loserContext = loserScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var winnerRepository = winnerScope.ServiceProvider.GetRequiredService<ISupplierRepository>();
        var loserRepository = loserScope.ServiceProvider.GetRequiredService<ISupplierRepository>();
        var winner = await winnerRepository.GetByIdAsync(supplier.Id);
        var loser = await loserRepository.GetByIdAsync(supplier.Id);
        var winnerTime = CreatedAt.AddHours(1);
        var loserTime = CreatedAt.AddHours(2);
        winner!.Deactivate(winnerTime);
        loser!.Deactivate(loserTime);

        await winnerContext.SaveChangesAsync();
        await Assert.ThrowsAsync<SupplierConcurrencyException>(() => loserContext.SaveChangesAsync());

        await using var verificationScope = fixture.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<SistemaGestionDbContext>();
        var persisted = await verificationContext.Suppliers
            .AsNoTracking()
            .SingleAsync(current => current.Id == supplier.Id);

        Assert.Equal(SupplierStatus.Inactive, persisted.Status);
        Assert.Equal(winnerTime, persisted.UpdatedAt);
    }

    private async Task PersistAsync(params Supplier[] suppliers)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.Suppliers.AddRange(suppliers);
        await context.SaveChangesAsync();
    }

    private static Supplier CreateSupplier(string supplierNumber) => new(
        Guid.NewGuid(), new SupplierNumber(supplierNumber), "Supplier", CreatedAt);
}
