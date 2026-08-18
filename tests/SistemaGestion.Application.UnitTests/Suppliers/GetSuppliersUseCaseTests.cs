using SistemaGestion.Application.Suppliers.GetSuppliers;
using SistemaGestion.Application.UnitTests.Suppliers.Fakes;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.UnitTests.Suppliers;

public sealed class GetSuppliersUseCaseTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Execute_WithDefaultQuery_UsesDefaultsAndReturnsTotalCount()
    {
        var repository = CreateRepository();
        var useCase = new GetSuppliersUseCase(repository);

        var result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, repository.RequestedPage);
        Assert.Equal(20, repository.RequestedPageSize);
    }

    [Fact]
    public async Task Execute_WithInvalidPagination_FallsBackToDefaults()
    {
        var repository = CreateRepository();
        var useCase = new GetSuppliersUseCase(repository);

        var result = await useCase.ExecuteAsync(new GetSuppliersQuery(0, -1));

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, repository.RequestedPage);
        Assert.Equal(20, repository.RequestedPageSize);
    }

    [Fact]
    public async Task Execute_WithPageSizeAboveMaximum_CapsAtOneHundred()
    {
        var repository = CreateRepository();
        var useCase = new GetSuppliersUseCase(repository);

        var result = await useCase.ExecuteAsync(new GetSuppliersQuery(2, 500));

        Assert.Equal(2, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(100, repository.RequestedPageSize);
    }

    [Fact]
    public async Task Execute_IncludesActiveAndInactiveWithNormalizedNumbersAndStatuses()
    {
        var repository = CreateRepository();
        var useCase = new GetSuppliersUseCase(repository);

        var result = await useCase.ExecuteAsync();

        Assert.Equal(["SUP-001", "SUP-002"],
            result.Items.Select(supplier => supplier.SupplierNumber));
        Assert.Contains(result.Items, supplier => supplier.Status == SupplierStatus.Active);
        Assert.Contains(result.Items, supplier => supplier.Status == SupplierStatus.Inactive);
    }

    private static FakeSupplierRepository CreateRepository()
    {
        var repository = new FakeSupplierRepository();
        var inactive = new Supplier(
            Guid.NewGuid(), new SupplierNumber("sup-002"), "Inactive", CreatedAt);
        inactive.Deactivate(CreatedAt.AddHours(1));
        repository.Suppliers.Add(inactive);
        repository.Suppliers.Add(new Supplier(
            Guid.NewGuid(), new SupplierNumber("sup-001"), "Active", CreatedAt));
        return repository;
    }
}
