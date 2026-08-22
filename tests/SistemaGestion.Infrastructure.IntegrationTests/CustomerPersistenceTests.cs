using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaGestion.Application.Customers.Persistence;
using SistemaGestion.Domain.Customers;
using SistemaGestion.Infrastructure.Persistence;

namespace SistemaGestion.Infrastructure.IntegrationTests;

public sealed class CustomerPersistenceTests : IClassFixture<SqlServerFixture>
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 22, 9, 30, 0, TimeSpan.Zero);

    private readonly SqlServerFixture fixture;

    public CustomerPersistenceTests(SqlServerFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Customer_round_trips_complete_profile_status_timestamps_and_rowversion()
    {
        var customer = new Customer(
            Guid.NewGuid(),
            new CustomerNumber("  cust-001  "),
            "Customer One",
            CreatedAt,
            "TAX-001",
            "customer@example.com",
            "+593 555 0100",
            "Quito, Ecuador");

        byte[] rowVersion;
        await using (var creationScope = fixture.CreateScope())
        {
            var creationContext = creationScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
            creationContext.Customers.Add(customer);
            await creationContext.SaveChangesAsync();
            rowVersion = creationContext.Entry(customer).Property<byte[]>("RowVersion").CurrentValue!;
        }

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var persisted = await repository.GetByIdAsync(customer.Id);
        var storedStatus = await context.Database
            .SqlQuery<string>($"SELECT [Status] AS [Value] FROM [Customers] WHERE [Id] = {customer.Id}")
            .SingleAsync();

        Assert.NotNull(persisted);
        Assert.Equal("CUST-001", persisted.CustomerNumber.Value);
        Assert.Equal("Customer One", persisted.Name);
        Assert.Equal("TAX-001", persisted.TaxIdentificationNumber);
        Assert.Equal("customer@example.com", persisted.Email);
        Assert.Equal("+593 555 0100", persisted.Phone);
        Assert.Equal("Quito, Ecuador", persisted.Address);
        Assert.Equal(CustomerStatus.Active, persisted.Status);
        Assert.Equal("Active", storedStatus);
        Assert.Equal(CreatedAt, persisted.CreatedAt);
        Assert.Equal(CreatedAt, persisted.UpdatedAt);
        Assert.NotEmpty(rowVersion);
    }

    [Fact]
    public async Task Normalized_duplicate_number_is_rejected_and_translated_specifically()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.Customers.Add(CreateCustomer("CUST-DUPLICATE"));
        context.Customers.Add(CreateCustomer("  cust-duplicate  "));

        await Assert.ThrowsAsync<CustomerDuplicateNumberException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Unrelated_unique_violation_is_not_translated_as_duplicate_customer_number()
    {
        var customer = CreateCustomer("CUST-PK-ONE");
        await PersistAsync(customer);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.Customers.Add(new Customer(
            customer.Id, new CustomerNumber("CUST-PK-TWO"), "Other", CreatedAt));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.IsNotType<CustomerDuplicateNumberException>(exception);
    }

    [Fact]
    public async Task Customer_page_is_newest_first_deterministic_paged_untracked_and_includes_all_statuses()
    {
        await using (var cleanupScope = fixture.CreateScope())
        {
            var cleanupContext = cleanupScope.ServiceProvider
                .GetRequiredService<SistemaGestionDbContext>();
            await cleanupContext.Customers.ExecuteDeleteAsync();
        }

        var oldest = CreateCustomer("CUST-OLD", CreatedAt);
        var tieFirst = new Customer(
            Guid.Parse("10000000-0000-0000-0000-000000000000"),
            new CustomerNumber("CUST-TIE-1"), "Customer", CreatedAt.AddHours(1));
        var tieSecond = new Customer(
            Guid.Parse("20000000-0000-0000-0000-000000000000"),
            new CustomerNumber("CUST-TIE-2"), "Customer", CreatedAt.AddHours(1));
        tieFirst.Deactivate(CreatedAt.AddHours(2));
        await PersistAsync(oldest, tieSecond, tieFirst);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var firstPage = await repository.GetPageAsync(1, 2);
        var secondPage = await repository.GetPageAsync(2, 2);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal([tieFirst.Id, tieSecond.Id], firstPage.Items.Select(customer => customer.Id));
        Assert.Equal(oldest.Id, Assert.Single(secondPage.Items).Id);
        Assert.Contains(firstPage.Items, customer => customer.Status == CustomerStatus.Active);
        Assert.Contains(firstPage.Items, customer => customer.Status == CustomerStatus.Inactive);
        Assert.Empty(context.ChangeTracker.Entries<Customer>());
    }

    [Fact]
    public async Task Concurrent_status_change_translates_conflict_changes_rowversion_and_preserves_winner()
    {
        var customer = CreateCustomer("CUST-CONCURRENCY");
        await PersistAsync(customer);

        await using var winnerScope = fixture.CreateScope();
        await using var loserScope = fixture.CreateScope();
        var winnerContext = winnerScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var loserContext = loserScope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var winner = await winnerScope.ServiceProvider.GetRequiredService<ICustomerRepository>()
            .GetByIdAsync(customer.Id);
        var loser = await loserScope.ServiceProvider.GetRequiredService<ICustomerRepository>()
            .GetByIdAsync(customer.Id);
        Assert.NotNull(winner);
        Assert.NotNull(loser);
        Assert.Equal(EntityState.Unchanged, winnerContext.Entry(winner).State);
        var initialRowVersion = winnerContext.Entry(winner)
            .Property<byte[]>("RowVersion").CurrentValue!.ToArray();
        var winnerTime = CreatedAt.AddHours(1);
        winner.Deactivate(winnerTime);
        loser.Deactivate(CreatedAt.AddHours(2));

        await winnerContext.SaveChangesAsync();
        var winnerRowVersion = winnerContext.Entry(winner)
            .Property<byte[]>("RowVersion").CurrentValue;
        await Assert.ThrowsAsync<CustomerConcurrencyException>(() => loserContext.SaveChangesAsync());

        await using var verificationScope = fixture.CreateScope();
        var persisted = await verificationScope.ServiceProvider
            .GetRequiredService<SistemaGestionDbContext>()
            .Customers.AsNoTracking()
            .SingleAsync(current => current.Id == customer.Id);

        Assert.NotNull(winnerRowVersion);
        Assert.NotEqual(initialRowVersion, winnerRowVersion);
        Assert.Equal(CustomerStatus.Inactive, persisted.Status);
        Assert.Equal(winnerTime, persisted.UpdatedAt);
    }

    [Fact]
    public async Task Customers_schema_migration_and_unique_index_match_contract()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var migrationExists = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260822080420_AddCustomers'")
            .SingleAsync();
        var columns = await context.Database.SqlQueryRaw<CustomerColumnMetadata>(
            "SELECT [COLUMN_NAME] AS [Name], [DATA_TYPE] AS [DataType], [CHARACTER_MAXIMUM_LENGTH] AS [MaximumLength], [IS_NULLABLE] AS [IsNullable] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_NAME] = 'Customers'")
            .ToListAsync();
        var uniqueIndexExists = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE [object_id] = OBJECT_ID('Customers') AND [name] = 'UX_Customers_CustomerNumber' AND [is_unique] = 1")
            .SingleAsync();
        var rowVersionPhysicalLength = await context.Database
            .SqlQuery<int>($"SELECT CAST([max_length] AS int) AS [Value] FROM sys.columns WHERE [object_id] = OBJECT_ID('Customers') AND [name] = 'RowVersion'")
            .SingleAsync();

        Assert.Equal(1, migrationExists);
        Assert.Equal(1, uniqueIndexExists);
        AssertColumn(columns, "Id", "uniqueidentifier", null, false);
        AssertColumn(columns, "CustomerNumber", "varchar", 50, false);
        AssertColumn(columns, "Name", "nvarchar", 200, false);
        AssertColumn(columns, "TaxIdentificationNumber", "nvarchar", 50, true);
        AssertColumn(columns, "Email", "nvarchar", 254, true);
        AssertColumn(columns, "Phone", "nvarchar", 50, true);
        AssertColumn(columns, "Address", "nvarchar", 500, true);
        AssertColumn(columns, "Status", "nvarchar", 20, false);
        AssertColumn(columns, "CreatedAt", "datetimeoffset", null, false);
        AssertColumn(columns, "UpdatedAt", "datetimeoffset", null, false);
        AssertColumn(columns, "RowVersion", "timestamp", null, false);
        Assert.Equal(8, rowVersionPhysicalLength);
    }

    private async Task PersistAsync(params Customer[] customers)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.Customers.AddRange(customers);
        await context.SaveChangesAsync();
    }

    private static Customer CreateCustomer(string number, DateTimeOffset? createdAt = null) =>
        new(Guid.NewGuid(), new CustomerNumber(number), "Customer", createdAt ?? CreatedAt);

    private static void AssertColumn(
        IReadOnlyCollection<CustomerColumnMetadata> columns,
        string name,
        string dataType,
        int? maximumLength,
        bool nullable)
    {
        var column = Assert.Single(columns, candidate => candidate.Name == name);
        Assert.Equal(dataType, column.DataType);
        Assert.Equal(maximumLength, column.MaximumLength);
        Assert.Equal(nullable ? "YES" : "NO", column.IsNullable);
    }
}

public sealed record CustomerColumnMetadata(
    string Name,
    string DataType,
    int? MaximumLength,
    string IsNullable);
