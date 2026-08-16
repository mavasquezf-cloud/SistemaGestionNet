using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaGestion.API.Contracts;
using SistemaGestion.Infrastructure.Persistence;

namespace SistemaGestion.API.IntegrationTests;

public sealed class CatalogApiTests : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient client;

    public CatalogApiTests(CatalogApiFactory factory)
    {
        client = factory.Client;
    }

    [Fact]
    public async Task Post_category_returns_created_and_get_contains_it()
    {
        var category = await CreateCategoryAsync("API categories");

        var response = await client.GetAsync("/api/categories");
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(categories!, item => item.Id == category.Id && item.Name == category.Name);
    }

    [Fact]
    public async Task Product_creation_normalizes_sku_and_product_gets_return_it()
    {
        var category = await CreateCategoryAsync("API product category");
        var createResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            "  api-sku-01  ", "API product", category.Id, "unit", 25.50m));
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("API-SKU-01", created.Sku);
        Assert.Equal($"/api/products/{created.Id}", createResponse.Headers.Location?.OriginalString);

        var detailResponse = await client.GetAsync($"/api/products/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ProductDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(created.Id, detail!.Id);
        Assert.Equal(category.Name, detail.CategoryName);

        var pageResponse = await client.GetAsync("/api/products?page=1&pageSize=20");
        var page = await pageResponse.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains(page!.Items, item => item.Id == created.Id);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task Missing_category_and_duplicate_normalized_sku_return_expected_problems()
    {
        var missingCategoryResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            $"MISSING-{Guid.NewGuid():N}", "Missing category", Guid.NewGuid(), "unit", 1m));
        Assert.Equal(HttpStatusCode.BadRequest, missingCategoryResponse.StatusCode);

        var category = await CreateCategoryAsync("Duplicate SKU category");
        var sku = $"duplicate-{Guid.NewGuid():N}";
        var firstResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            sku, "First duplicate", category.Id, "unit", 1m));
        var duplicateResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            $"  {sku.ToUpperInvariant()}  ", "Second duplicate", category.Id, "unit", 2m));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Unknown_product_and_negative_price_return_expected_errors()
    {
        var unknownResponse = await client.GetAsync($"/api/products/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);

        var category = await CreateCategoryAsync("Validation category");
        var invalidResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            $"NEGATIVE-{Guid.NewGuid():N}", "Invalid product", category.Id, "unit", -0.01m));
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_exposes_all_catalog_routes()
    {
        var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(paths.TryGetProperty("/api/categories", out var categories));
        Assert.True(categories.TryGetProperty("post", out _));
        Assert.True(categories.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/products", out var products));
        Assert.True(products.TryGetProperty("post", out _));
        Assert.True(products.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/products/{id}", out var product));
        Assert.True(product.TryGetProperty("get", out _));
    }

    private async Task<CategoryResponse> CreateCategoryAsync(string name)
    {
        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name));
        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(category);
        Assert.NotNull(response.Headers.Location);
        return category;
    }
}

public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ServerConnection =
        "Server=DESKTOP-HOGNLH6\\SQL2025;Trusted_Connection=True;TrustServerCertificate=True;";

    private readonly string databaseName = $"SistemaGestionNet_ApiTests_{Guid.NewGuid():N}";

    public HttpClient Client { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SistemaGestionDb"] = $"{ServerConnection}Database={databaseName};"
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Client.Dispose();
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
        await context.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}
