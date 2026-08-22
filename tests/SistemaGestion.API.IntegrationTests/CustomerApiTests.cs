using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaGestion.API.Contracts;
using SistemaGestion.Infrastructure.Persistence;

namespace SistemaGestion.API.IntegrationTests;

public sealed class CustomerApiTests : IClassFixture<CatalogApiFactory>
{
    private readonly CatalogApiFactory factory;
    private readonly HttpClient client;

    public CustomerApiTests(CatalogApiFactory factory)
    {
        this.factory = factory;
        client = factory.Client;
    }

    [Fact]
    public async Task Customer_lifecycle_returns_snapshots_and_is_idempotent()
    {
        var number = $"CUST-{Guid.NewGuid():N}";
        var createResponse = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest(
            $"  {number.ToLowerInvariant()}  ",
            "Customer One",
            "1790012345001",
            "customer@example.com",
            "+593 2 555 0100",
            "Quito, Ecuador"));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(number.ToUpperInvariant(), created.CustomerNumber);
        Assert.Equal("Customer One", created.Name);
        Assert.Equal("1790012345001", created.TaxIdentificationNumber);
        Assert.Equal("customer@example.com", created.Email);
        Assert.Equal("+593 2 555 0100", created.Phone);
        Assert.Equal("Quito, Ecuador", created.Address);
        Assert.Equal("Active", created.Status);
        Assert.NotEqual(default, created.CreatedAt);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
        Assert.Equal($"/api/customers/{created.Id}", createResponse.Headers.Location?.OriginalString);

        var activeDetailResponse = await client.GetAsync($"/api/customers/{created.Id}");
        var activeDetail = await activeDetailResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.Equal(HttpStatusCode.OK, activeDetailResponse.StatusCode);
        Assert.Equal(created, activeDetail);

        var inactiveResponse = await ChangeStatusAsync(created.Id, "Inactive");
        var inactive = await inactiveResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);
        Assert.NotNull(inactive);
        Assert.Equal("Inactive", inactive.Status);
        Assert.True(inactive.UpdatedAt >= created.UpdatedAt);

        var inactiveDetailResponse = await client.GetAsync($"/api/customers/{created.Id}");
        var inactiveDetail = await inactiveDetailResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.Equal(HttpStatusCode.OK, inactiveDetailResponse.StatusCode);
        Assert.Equal("Inactive", inactiveDetail!.Status);

        var repeatedResponse = await ChangeStatusAsync(created.Id, "Inactive");
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(inactive.UpdatedAt, repeated!.UpdatedAt);

        var activeResponse = await ChangeStatusAsync(created.Id, "Active");
        var active = await activeResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        Assert.Equal("Active", active!.Status);
        Assert.True(active.UpdatedAt >= repeated.UpdatedAt);
    }

    [Fact]
    public async Task Duplicate_normalized_customer_number_returns_conflict()
    {
        var number = $"CUST-{Guid.NewGuid():N}";
        var firstResponse = await client.PostAsJsonAsync(
            "/api/customers", new CreateCustomerRequest(number, "First Customer"));
        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest($"  {number.ToLowerInvariant()}  ", "Duplicate"));

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
            "missing-number" => new { name = "Customer" },
            "missing-name" => new { customerNumber = $"CUST-{Guid.NewGuid():N}" },
            "invalid-email" => new
            {
                customerNumber = $"CUST-{Guid.NewGuid():N}",
                name = "Customer",
                email = "not-an-email"
            },
            _ => throw new InvalidOperationException()
        };

        var response = await client.PostAsJsonAsync("/api/customers", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_customer_detail_and_status_change_return_not_found()
    {
        var id = Guid.NewGuid();

        var detailResponse = await client.GetAsync($"/api/customers/{id}");
        var statusResponse = await ChangeStatusAsync(id, "Inactive");

        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, statusResponse.StatusCode);
    }

    [Theory]
    [InlineData("{\"status\":\"Suspended\"}")]
    [InlineData("{\"status\":1}")]
    public async Task Invalid_status_value_returns_bad_request(string json)
    {
        var customer = await CreateCustomerAsync();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PatchAsync($"/api/customers/{customer.Id}/status", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customer_page_normalizes_parameters_counts_owned_data_and_includes_all_statuses()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SistemaGestionDbContext>();
            await context.Customers.ExecuteDeleteAsync();
        }

        var first = await CreateCustomerAsync();
        var second = await CreateCustomerAsync();
        var third = await CreateCustomerAsync();
        var inactiveResponse = await ChangeStatusAsync(second.Id, "Inactive");
        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);

        var response = await client.GetAsync("/api/customers?page=0&pageSize=101");
        var page = await response.Content.ReadFromJsonAsync<PagedCustomersResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
        Assert.Contains(page.Items, customer => customer.Id == first.Id && customer.Status == "Active");
        Assert.Contains(page.Items, customer => customer.Id == second.Id && customer.Status == "Inactive");
        Assert.Contains(page.Items, customer => customer.Id == third.Id && customer.Status == "Active");
    }

    [Fact]
    public async Task OpenApi_exposes_customer_routes_and_safe_request_schemas()
    {
        var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(paths.TryGetProperty("/api/customers", out var customers));
        Assert.True(customers.TryGetProperty("post", out _));
        Assert.True(customers.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/customers/{id}", out var customer));
        Assert.True(customer.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/customers/{id}/status", out var status));
        Assert.True(status.TryGetProperty("patch", out _));

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var createProperties = schemas.GetProperty(nameof(CreateCustomerRequest))
            .GetProperty("properties");
        Assert.True(createProperties.TryGetProperty("customerNumber", out _));
        Assert.True(createProperties.TryGetProperty("name", out _));
        Assert.True(createProperties.TryGetProperty("taxIdentificationNumber", out _));
        Assert.True(createProperties.TryGetProperty("email", out _));
        Assert.True(createProperties.TryGetProperty("phone", out _));
        Assert.True(createProperties.TryGetProperty("address", out _));
        Assert.False(createProperties.TryGetProperty("id", out _));
        Assert.False(createProperties.TryGetProperty("status", out _));
        Assert.False(createProperties.TryGetProperty("createdAt", out _));
        Assert.False(createProperties.TryGetProperty("updatedAt", out _));
        Assert.False(createProperties.TryGetProperty("rowVersion", out _));

        var statusProperties = schemas.GetProperty(nameof(ChangeCustomerStatusRequest))
            .GetProperty("properties");
        var statusProperty = Assert.Single(statusProperties.EnumerateObject());
        Assert.Equal("status", statusProperty.Name);
        Assert.Equal("string", statusProperty.Value.GetProperty("type").GetString());
    }

    private async Task<CustomerResponse> CreateCustomerAsync()
    {
        var response = await client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest($"CUST-{Guid.NewGuid():N}", "Customer"));
        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return customer!;
    }

    private Task<HttpResponseMessage> ChangeStatusAsync(Guid customerId, string status) =>
        client.PatchAsJsonAsync(
            $"/api/customers/{customerId}/status",
            new ChangeCustomerStatusRequest(status));
}
