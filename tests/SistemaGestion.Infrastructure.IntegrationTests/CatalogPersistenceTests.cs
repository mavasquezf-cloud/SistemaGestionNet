using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Domain.Catalog.Categories;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Infrastructure;
using SistemaGestion.Infrastructure.Persistence;

namespace SistemaGestion.Infrastructure.IntegrationTests;

public sealed class CatalogPersistenceTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture fixture;

    public CatalogPersistenceTests(SqlServerFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Category_can_be_persisted_and_read_without_tracking()
    {
        await using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var category = new Category(Guid.NewGuid(), "Hardware", "Physical products");

        await repository.AddAsync(category);
        await unitOfWork.SaveChangesAsync();

        var categories = await repository.GetAllAsync();

        var persisted = Assert.Single(categories, item => item.Id == category.Id);
        Assert.Equal("Hardware", persisted.Name);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task Product_and_normalized_sku_round_trip_with_category_name()
    {
        await using var scope = fixture.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var category = new Category(Guid.NewGuid(), "Peripherals");
        var product = new Product(
            Guid.NewGuid(), new Sku("  kb-100  "), "Keyboard", category.Id, "unit", 49.95m);

        await categories.AddAsync(category);
        await products.AddAsync(product);
        await unitOfWork.SaveChangesAsync();

        var persisted = await products.GetByIdAsync(product.Id);

        Assert.NotNull(persisted);
        Assert.Equal("KB-100", persisted.Product.Sku.Value);
        Assert.Equal(49.95m, persisted.Product.DefaultSalePrice);
        Assert.Equal("Peripherals", persisted.CategoryName);
        Assert.True(await products.ExistsBySkuAsync(new Sku("kb-100")));
    }

    [Fact]
    public async Task Duplicate_normalized_sku_is_rejected_by_unique_index()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        var category = new Category(Guid.NewGuid(), "Unique SKU category");
        context.Categories.Add(category);
        context.Products.Add(new Product(
            Guid.NewGuid(), new Sku("unique-01"), "First", category.Id, "unit", 1m));
        await context.SaveChangesAsync();

        context.Products.Add(new Product(
            Guid.NewGuid(), new Sku(" UNIQUE-01 "), "Second", category.Id, "unit", 2m));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Product_foreign_key_is_enforced()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        context.Products.Add(new Product(
            Guid.NewGuid(), new Sku($"ORPHAN-{Guid.NewGuid():N}"), "Orphan", Guid.NewGuid(), "unit", 1m));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string ServerConnection =
        "Server=DESKTOP-HOGNLH6\\SQL2025;Trusted_Connection=True;TrustServerCertificate=True;";

    private readonly string databaseName = $"SistemaGestionNet_IntegrationTests_{Guid.NewGuid():N}";
    private ServiceProvider? serviceProvider;

    public async Task InitializeAsync()
    {
        var connectionString = $"{ServerConnection}Database={databaseName};";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SistemaGestionDb"] = connectionString
            })
            .Build();

        serviceProvider = new ServiceCollection()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();

        await using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        await context.Database.MigrateAsync();
    }

    public AsyncServiceScope CreateScope()
    {
        return (serviceProvider ?? throw new InvalidOperationException("Fixture is not initialized."))
            .CreateAsyncScope();
    }

    public async Task DisposeAsync()
    {
        if (serviceProvider is null)
        {
            return;
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
            await context.Database.EnsureDeletedAsync();
        }

        await serviceProvider.DisposeAsync();
    }
}
