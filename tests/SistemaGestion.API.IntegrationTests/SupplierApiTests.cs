using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SistemaGestion.API.Contracts;

namespace SistemaGestion.API.IntegrationTests;

public sealed class SupplierApiTests : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient client;

    public SupplierApiTests(CatalogApiFactory factory)
    {
        client = factory.Client;
    }

    [Fact]
    public async Task Supplier_lifecycle_is_queryable_paged_and_idempotent()
    {
        var createResponse = await client.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest(
            $"  sup-{Guid.NewGuid():N}  ",
            "Tech Distributor S.A.",
            "1790012345001",
            "sales@techdistributor.com",
            "+593 2 555 0100",
            "Quito, Ecuador"));
        var created = await createResponse.Content.ReadFromJsonAsync<SupplierResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal($"/api/suppliers/{created.Id}", createResponse.Headers.Location?.OriginalString);
        Assert.StartsWith("SUP-", created.SupplierNumber);
        Assert.Equal("Active", created.Status);
        Assert.NotEqual(default, created.CreatedAt);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);

        var detailResponse = await client.GetAsync($"/api/suppliers/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(created, detail);

        var inactiveResponse = await ChangeStatusAsync(created.Id, "Inactive");
        var inactive = await inactiveResponse.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);
        Assert.NotNull(inactive);
        Assert.Equal("Inactive", inactive.Status);
        Assert.True(inactive.UpdatedAt >= created.UpdatedAt);

        var inactiveDetailResponse = await client.GetAsync($"/api/suppliers/{created.Id}");
        var inactiveDetail = await inactiveDetailResponse.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.Equal(HttpStatusCode.OK, inactiveDetailResponse.StatusCode);
        Assert.Equal("Inactive", inactiveDetail!.Status);

        var listResponse = await client.GetAsync("/api/suppliers?page=1&pageSize=20");
        var page = await listResponse.Content.ReadFromJsonAsync<PagedSuppliersResponse>();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.True(page.TotalCount >= 1);
        Assert.Contains(page.Items, supplier =>
            supplier.Id == created.Id && supplier.Status == "Inactive");

        var repeatedResponse = await ChangeStatusAsync(created.Id, "Inactive");
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(inactive.UpdatedAt, repeated!.UpdatedAt);

        var activeResponse = await ChangeStatusAsync(created.Id, "Active");
        var active = await activeResponse.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        Assert.Equal("Active", active!.Status);
        Assert.True(active.UpdatedAt >= repeated.UpdatedAt);
    }

    [Fact]
    public async Task Duplicate_normalized_supplier_number_returns_conflict()
    {
        var number = $"SUP-{Guid.NewGuid():N}";
        var firstResponse = await client.PostAsJsonAsync(
            "/api/suppliers", new CreateSupplierRequest(number, "First Supplier"));
        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/suppliers", new CreateSupplierRequest($"  {number.ToLowerInvariant()}  ", "Duplicate"));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Theory]
    [InlineData("missing-number")]
    [InlineData("missing-name")]
    [InlineData("invalid-email")]
    public async Task Invalid_create_request_returns_bad_request(string scenario)
    {
        object request = scenario switch
        {
            "missing-number" => new { name = "Supplier" },
            "missing-name" => new { supplierNumber = $"SUP-{Guid.NewGuid():N}" },
            "invalid-email" => new
            {
                supplierNumber = $"SUP-{Guid.NewGuid():N}",
                name = "Supplier",
                email = "not-an-email"
            },
            _ => throw new InvalidOperationException()
        };

        var response = await client.PostAsJsonAsync("/api/suppliers", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_supplier_detail_and_status_change_return_not_found()
    {
        var id = Guid.NewGuid();

        var detailResponse = await client.GetAsync($"/api/suppliers/{id}");
        var statusResponse = await ChangeStatusAsync(id, "Inactive");

        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, statusResponse.StatusCode);
    }

    [Theory]
    [InlineData("{\"status\":\"Suspended\"}")]
    [InlineData("{\"status\":1}")]
    public async Task Invalid_status_value_returns_bad_request(string json)
    {
        var supplier = await CreateSupplierAsync();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PatchAsync($"/api/suppliers/{supplier.Id}/status", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_exposes_supplier_routes_and_safe_create_schema()
    {
        var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(paths.TryGetProperty("/api/suppliers", out var suppliers));
        Assert.True(suppliers.TryGetProperty("post", out _));
        Assert.True(suppliers.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/suppliers/{id}", out var supplier));
        Assert.True(supplier.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/suppliers/{id}/status", out var status));
        Assert.True(status.TryGetProperty("patch", out _));

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var createSchema = schemas.GetProperty(nameof(CreateSupplierRequest));
        var properties = createSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("supplierNumber", out _));
        Assert.True(properties.TryGetProperty("name", out _));
        Assert.False(properties.TryGetProperty("id", out _));
        Assert.False(properties.TryGetProperty("status", out _));
        Assert.False(properties.TryGetProperty("createdAt", out _));
        Assert.False(properties.TryGetProperty("updatedAt", out _));
        Assert.False(properties.TryGetProperty("rowVersion", out _));
    }

    private async Task<SupplierResponse> CreateSupplierAsync()
    {
        var response = await client.PostAsJsonAsync(
            "/api/suppliers",
            new CreateSupplierRequest($"SUP-{Guid.NewGuid():N}", "Supplier"));
        var supplier = await response.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return supplier!;
    }

    private Task<HttpResponseMessage> ChangeStatusAsync(Guid supplierId, string status) =>
        client.PatchAsJsonAsync(
            $"/api/suppliers/{supplierId}/status",
            new ChangeSupplierStatusRequest(status));
}
