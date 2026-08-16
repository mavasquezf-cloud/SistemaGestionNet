using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaGestion.API.Contracts;
using SistemaGestion.Infrastructure.Persistence;

namespace SistemaGestion.API.IntegrationTests;

public sealed class InventoryApiTests : IClassFixture<CatalogApiFactory>
{
    private readonly CatalogApiFactory factory;
    private readonly HttpClient client;

    public InventoryApiTests(CatalogApiFactory factory)
    {
        this.factory = factory;
        client = factory.Client;
    }

    [Fact]
    public async Task Adjustment_lifecycle_preserves_balance_history_order_and_pagination()
    {
        var product = await CreateProductAsync("INVENTORY-LIFECYCLE");

        var initialInventory = await GetInventoryAsync(product.Id);
        Assert.Equal(0m, initialInventory.QuantityOnHand);

        var increaseResponse = await AdjustAsync(
            product.Id, new ManualInventoryAdjustmentRequest(10m, "Initial inventory", "INITIAL-001"));
        var increase = await increaseResponse.Content.ReadFromJsonAsync<InventoryAdjustmentResponse>();
        Assert.True(
            increaseResponse.StatusCode == HttpStatusCode.Created,
            $"Expected Created but received {increaseResponse.StatusCode}: {await increaseResponse.Content.ReadAsStringAsync()}");
        Assert.NotNull(increase);
        Assert.Equal(product.Id, increase.ProductId);
        Assert.Equal(10m, increase.QuantityDelta);
        Assert.Equal(10m, increase.QuantityOnHand);
        Assert.Equal("Increase", increase.Type);
        Assert.Equal("ManualAdjustment", increase.Source);
        Assert.Equal("INITIAL-001", increase.Reference);
        Assert.Equal($"/api/inventory/{product.Id}/movements", increaseResponse.Headers.Location?.OriginalString);
        Assert.Equal(10m, (await GetInventoryAsync(product.Id)).QuantityOnHand);

        var decreaseResponse = await AdjustAsync(
            product.Id, new ManualInventoryAdjustmentRequest(-3m, "Damaged units", "DAMAGE-001"));
        var decrease = await decreaseResponse.Content.ReadFromJsonAsync<InventoryAdjustmentResponse>();
        Assert.Equal(HttpStatusCode.Created, decreaseResponse.StatusCode);
        Assert.NotNull(decrease);
        Assert.Equal(-3m, decrease.QuantityDelta);
        Assert.Equal(7m, decrease.QuantityOnHand);
        Assert.Equal("Decrease", decrease.Type);
        Assert.Equal(7m, (await GetInventoryAsync(product.Id)).QuantityOnHand);

        var rejectedResponse = await AdjustAsync(
            product.Id, new ManualInventoryAdjustmentRequest(-8m, "Would go negative"));
        Assert.Equal(HttpStatusCode.BadRequest, rejectedResponse.StatusCode);
        Assert.Equal(7m, (await GetInventoryAsync(product.Id)).QuantityOnHand);

        var historyResponse = await client.GetAsync(
            $"/api/inventory/{product.Id}/movements?page=1&pageSize=50");
        var history = await historyResponse.Content
            .ReadFromJsonAsync<PagedInventoryMovementsResponse>();
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.NotNull(history);
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(1, history.Page);
        Assert.Equal(50, history.PageSize);
        Assert.Equal([decrease.MovementId, increase.MovementId],
            history.Items.Select(movement => movement.Id));
        Assert.Equal([-3m, 10m], history.Items.Select(movement => movement.QuantityDelta));

        var pagedResponse = await client.GetAsync(
            $"/api/inventory/{product.Id}/movements?page=2&pageSize=1");
        var page = await pagedResponse.Content.ReadFromJsonAsync<PagedInventoryMovementsResponse>();
        Assert.Equal(HttpStatusCode.OK, pagedResponse.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(increase.MovementId, Assert.Single(page.Items).Id);
    }

    [Theory]
    [InlineData(0, "Valid reason")]
    [InlineData(1, "")]
    [InlineData(1, "   ")]
    public async Task Invalid_adjustment_request_returns_bad_request(
        decimal quantityDelta,
        string reason)
    {
        var product = await CreateProductAsync($"INVALID-{Guid.NewGuid():N}");

        var response = await AdjustAsync(
            product.Id, new ManualInventoryAdjustmentRequest(quantityDelta, reason));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0m, (await GetInventoryAsync(product.Id)).QuantityOnHand);
    }

    [Fact]
    public async Task Missing_product_adjustment_and_inventory_query_return_not_found()
    {
        var productId = Guid.NewGuid();

        var adjustmentResponse = await AdjustAsync(
            productId, new ManualInventoryAdjustmentRequest(1m, "Count"));
        var inventoryResponse = await client.GetAsync($"/api/inventory/{productId}");

        Assert.Equal(HttpStatusCode.NotFound, adjustmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inventoryResponse.StatusCode);
    }

    [Fact]
    public async Task Inactive_product_rejects_adjustment_but_inventory_remains_queryable()
    {
        var product = await CreateProductAsync("INACTIVE-INVENTORY");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
            var persisted = await context.Products.SingleAsync(item => item.Id == product.Id);
            persisted.Deactivate();
            await context.SaveChangesAsync();
        }

        var adjustmentResponse = await AdjustAsync(
            product.Id, new ManualInventoryAdjustmentRequest(1m, "Count"));
        var inventoryResponse = await client.GetAsync($"/api/inventory/{product.Id}");
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<InventoryResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, adjustmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inventoryResponse.StatusCode);
        Assert.Equal(0m, inventory!.QuantityOnHand);
    }

    [Fact]
    public async Task OpenApi_exposes_all_inventory_routes()
    {
        var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(paths.TryGetProperty("/api/inventory/{productId}/adjustments", out var adjustments));
        Assert.True(adjustments.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/inventory/{productId}", out var inventory));
        Assert.True(inventory.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/inventory/{productId}/movements", out var movements));
        Assert.True(movements.TryGetProperty("get", out _));
    }

    [Fact]
    public async Task Product_API_responses_do_not_expose_inventory_fields()
    {
        var product = await CreateProductAsync("NO-STOCK-FIELD");

        var detailResponse = await client.GetAsync($"/api/products/{product.Id}");
        using var detail = JsonDocument.Parse(await detailResponse.Content.ReadAsStreamAsync());
        var listResponse = await client.GetAsync("/api/products?page=1&pageSize=100");
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStreamAsync());
        var listedProduct = list.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == product.Id);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.False(detail.RootElement.TryGetProperty("stock", out _));
        Assert.False(detail.RootElement.TryGetProperty("quantityOnHand", out _));
        Assert.False(listedProduct.TryGetProperty("stock", out _));
        Assert.False(listedProduct.TryGetProperty("quantityOnHand", out _));
    }

    private async Task<ProductResponse> CreateProductAsync(string skuPrefix)
    {
        var shortPrefix = skuPrefix.Length <= 20 ? skuPrefix : skuPrefix[..20];
        var categoryResponse = await client.PostAsJsonAsync(
            "/api/categories", new CreateCategoryRequest($"Category {Guid.NewGuid():N}"));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);

        var productResponse = await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(
                $"{shortPrefix}-{Guid.NewGuid():N}", "Inventory product", category!.Id, "unit", 1m));
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        return product!;
    }

    private async Task<InventoryResponse> GetInventoryAsync(Guid productId)
    {
        var response = await client.GetAsync($"/api/inventory/{productId}");
        var inventory = await response.Content.ReadFromJsonAsync<InventoryResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return inventory!;
    }

    private Task<HttpResponseMessage> AdjustAsync(
        Guid productId,
        ManualInventoryAdjustmentRequest request) =>
        client.PostAsJsonAsync($"/api/inventory/{productId}/adjustments", request);
}
