using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SistemaGestion.API.Contracts;

namespace SistemaGestion.API.IntegrationTests;

public sealed class PurchasingApiTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient client = factory.Client;

    [Fact]
    public async Task Complete_purchase_workflow_snapshots_values_receives_once_and_is_queryable()
    {
        static async Task<string> RequireResponseAsync(
            HttpResponseMessage response,
            HttpStatusCode expectedStatus,
            string operation,
            bool requireStringPurchaseStatus = false)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != expectedStatus)
            {
                Assert.Fail(
                    $"Operation: {operation}{Environment.NewLine}" +
                    $"Expected status: {(int)expectedStatus} ({expectedStatus}){Environment.NewLine}" +
                    $"Actual status: {(int)response.StatusCode} ({response.StatusCode}){Environment.NewLine}" +
                    $"Body: {body}");
            }

            if (requireStringPurchaseStatus)
            {
                using var document = JsonDocument.Parse(body);
                if (!document.RootElement.TryGetProperty("status", out var status)
                    || status.ValueKind != JsonValueKind.String)
                {
                    Assert.Fail(
                        $"Operation: {operation}{Environment.NewLine}" +
                        $"Expected JSON status token: String{Environment.NewLine}" +
                        $"Actual JSON status token: {(document.RootElement.TryGetProperty("status", out status) ? status.ValueKind : "Missing")}{Environment.NewLine}" +
                        $"Body: {body}");
                }
            }

            return body;
        }

        var supplier = await CreateSupplier();
        var product = await CreateProduct("Purchased widget", "box");

        var createResponse = await client.PostAsJsonAsync(
            "/api/purchases",
            new CreatePurchaseRequest(supplier.Id, "SUP-INV-1"));
        var createBody = await RequireResponseAsync(
            createResponse, HttpStatusCode.Created, "CreatePurchase", requireStringPurchaseStatus: true);
        var purchase = JsonSerializer.Deserialize<PurchaseResponse>(createBody, JsonSerializerOptions.Web);
        Assert.NotNull(purchase);
        Assert.Matches("^PUR-[0-9]{8}$", purchase.PurchaseNumber);
        Assert.Equal(supplier.Name, purchase.SupplierName);
        Assert.Equal(
            $"/api/purchases/{purchase.Id}",
            createResponse.Headers.Location?.OriginalString);

        var addLineResponse = await client.PostAsJsonAsync(
            $"/api/purchases/{purchase.Id}/lines",
            new AddPurchaseLineRequest(product.Id, 2.1256m, 3.4567m));
        var addLineBody = await RequireResponseAsync(
            addLineResponse, HttpStatusCode.Created, "AddLine", requireStringPurchaseStatus: true);
        purchase = JsonSerializer.Deserialize<PurchaseResponse>(addLineBody, JsonSerializerOptions.Web);
        Assert.NotNull(purchase);
        var line = Assert.Single(purchase.Lines);
        Assert.Equal(product.Name, line.ProductName);
        Assert.Equal("box", line.UnitOfMeasure);
        Assert.Equal(7.3476m, line.LineTotal);
        Assert.Equal(line.LineTotal, purchase.Total);

        var duplicateLineResponse = await client.PostAsJsonAsync(
            $"/api/purchases/{purchase.Id}/lines",
            new AddPurchaseLineRequest(product.Id, 1m, 1m));
        await RequireResponseAsync(duplicateLineResponse, HttpStatusCode.Conflict, "DuplicateLine");

        var confirmResponse = await client.PostAsync(
            $"/api/purchases/{purchase.Id}/confirm", null);
        await RequireResponseAsync(
            confirmResponse, HttpStatusCode.OK, "Confirm", requireStringPurchaseStatus: true);

        var inventoryBeforeReceiptResponse = await client.GetAsync($"/api/inventory/{product.Id}");
        var inventoryBeforeReceiptBody = await RequireResponseAsync(
            inventoryBeforeReceiptResponse, HttpStatusCode.OK, "InventoryBeforeReceipt");
        var inventoryBeforeReceipt = JsonSerializer.Deserialize<InventoryResponse>(
            inventoryBeforeReceiptBody, JsonSerializerOptions.Web);
        Assert.Equal(0m, inventoryBeforeReceipt!.QuantityOnHand);

        var receiveResponse = await client.PostAsync(
            $"/api/purchases/{purchase.Id}/receive", null);
        var receiveBody = await RequireResponseAsync(
            receiveResponse, HttpStatusCode.OK, "Receive", requireStringPurchaseStatus: true);
        purchase = JsonSerializer.Deserialize<PurchaseResponse>(receiveBody, JsonSerializerOptions.Web);
        Assert.NotNull(purchase);
        Assert.Equal("Received", purchase.Status);
        Assert.NotNull(purchase.ReceivedAt);

        var secondReceiveResponse = await client.PostAsync(
            $"/api/purchases/{purchase.Id}/receive", null);
        await RequireResponseAsync(secondReceiveResponse, HttpStatusCode.Conflict, "SecondReceive");

        var detailResponse = await client.GetAsync($"/api/purchases/{purchase.Id}");
        var detailBody = await RequireResponseAsync(
            detailResponse, HttpStatusCode.OK, "Detail", requireStringPurchaseStatus: true);
        var detail = JsonSerializer.Deserialize<PurchaseResponse>(detailBody, JsonSerializerOptions.Web);
        Assert.Single(detail!.Lines);

        var pageResponse = await client.GetAsync("/api/purchases?page=0&pageSize=500");
        var pageBody = await RequireResponseAsync(pageResponse, HttpStatusCode.OK, "PurchasesPage");
        var page = JsonSerializer.Deserialize<PagedPurchasesResponse>(pageBody, JsonSerializerOptions.Web);
        Assert.Equal(1, page!.Page); Assert.Equal(100, page.PageSize); Assert.Contains(page.Items, x => x.Id == purchase.Id);
        var inventoryResponse = await client.GetAsync($"/api/inventory/{product.Id}");
        var inventoryBody = await RequireResponseAsync(inventoryResponse, HttpStatusCode.OK, "InventoryAfterReceipt");
        var inventory = JsonSerializer.Deserialize<InventoryResponse>(inventoryBody, JsonSerializerOptions.Web); Assert.Equal(2.1256m, inventory!.QuantityOnHand);
        var movementsResponse = await client.GetAsync($"/api/inventory/{product.Id}/movements");
        var movementsBody = await RequireResponseAsync(movementsResponse, HttpStatusCode.OK, "InventoryMovements");
        var movements = JsonSerializer.Deserialize<PagedInventoryMovementsResponse>(movementsBody, JsonSerializerOptions.Web);
        var receipt = Assert.Single(movements!.Items, x => x.Source == "PurchaseReceipt"); Assert.Equal(purchase.PurchaseNumber, receipt.Reference);
    }

    [Fact]
    public async Task Purchasing_rejections_and_validation_map_to_expected_status_codes()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/purchases", new CreatePurchaseRequest(Guid.NewGuid()))).StatusCode);
        var supplier = await CreateSupplier(); var empty = await CreatePurchase(supplier.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/purchases/{empty.Id}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/purchases/{empty.Id}/receive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/purchases/{empty.Id}/lines", new AddPurchaseLineRequest(Guid.NewGuid(), 1, 1))).StatusCode);
        var product = await CreateProduct("Validation product", "unit");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/purchases/{empty.Id}/lines", new AddPurchaseLineRequest(product.Id, 0, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/purchases/{empty.Id}/lines", new AddPurchaseLineRequest(product.Id, 1, -1))).StatusCode);
        var cancel = await client.PostAsync($"/api/purchases/{empty.Id}/cancel", null); Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/purchases/{empty.Id}/receive", null)).StatusCode);
    }

    [Fact]
    public async Task OpenApi_exposes_seven_routes_and_safe_request_schemas()
    {
        var response = await client.GetAsync("/openapi/v1.json"); using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement; var paths = root.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/purchases").TryGetProperty("post", out _)); Assert.True(paths.GetProperty("/api/purchases").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/purchases/{id}").TryGetProperty("get", out _)); Assert.True(paths.GetProperty("/api/purchases/{id}/lines").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/purchases/{id}/confirm").TryGetProperty("post", out _)); Assert.True(paths.GetProperty("/api/purchases/{id}/receive").TryGetProperty("post", out _)); Assert.True(paths.GetProperty("/api/purchases/{id}/cancel").TryGetProperty("post", out _));
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var create = schemas.GetProperty(nameof(CreatePurchaseRequest)).GetProperty("properties");
        Assert.True(create.TryGetProperty("supplierId", out _)); Assert.True(create.TryGetProperty("supplierDocumentReference", out _));
        foreach (var protectedName in new[] { "purchaseNumber", "supplierName", "status", "total", "createdAt", "updatedAt", "receivedAt", "lines" }) Assert.False(create.TryGetProperty(protectedName, out _));
        var line = schemas.GetProperty(nameof(AddPurchaseLineRequest)).GetProperty("properties");
        Assert.True(line.TryGetProperty("productId", out _)); Assert.True(line.TryGetProperty("quantity", out _)); Assert.True(line.TryGetProperty("unitCost", out _));
        Assert.False(line.TryGetProperty("productName", out _)); Assert.False(line.TryGetProperty("unitOfMeasure", out _)); Assert.False(line.TryGetProperty("lineTotal", out _));
    }

    private async Task<SupplierResponse> CreateSupplier()
    {
        var response = await client.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest($"SUP-{Guid.NewGuid():N}", "Purchasing supplier"));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<SupplierResponse>())!;
    }
    private async Task<ProductResponse> CreateProduct(string name, string unit)
    {
        var categoryResponse = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest($"Purchase-{Guid.NewGuid():N}"));
        categoryResponse.EnsureSuccessStatusCode();
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>();
        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest($"SKU-{Guid.NewGuid():N}", name, category!.Id, unit, 1));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }
    private async Task<PurchaseResponse> CreatePurchase(Guid supplierId)
    {
        var response = await client.PostAsJsonAsync("/api/purchases", new CreatePurchaseRequest(supplierId)); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PurchaseResponse>())!;
    }
}
