using SistemaGestion.Application.Suppliers.CreateSupplier;
using SistemaGestion.Application.Suppliers.GetSupplierById;
using SistemaGestion.Application.UnitTests.Suppliers.Fakes;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.UnitTests.Suppliers;

public sealed class CreateAndGetSupplierUseCaseTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_WithUniqueNumber_NormalizesPersistsAndUsesClock()
    {
        var repository = new FakeSupplierRepository();
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new CreateSupplierUseCase(
            repository, unitOfWork, new FakeClock(FixedTime));

        var result = await useCase.ExecuteAsync(new CreateSupplierCommand(
            "  sup-001  ",
            "  Example Supplier  ",
            "  TAX-1  ",
            "  sales@example.com  ",
            "  +593 555 0100  ",
            "  Quito  "));

        Assert.Equal(CreateSupplierOutcome.Success, result.Outcome);
        Assert.NotNull(result.Supplier);
        Assert.Equal("SUP-001", result.Supplier.SupplierNumber);
        Assert.Equal("Example Supplier", result.Supplier.Name);
        Assert.Equal(FixedTime, result.Supplier.CreatedAt);
        Assert.Equal(FixedTime, result.Supplier.UpdatedAt);
        Assert.Equal(SupplierStatus.Active, result.Supplier.Status);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal("SUP-001", Assert.Single(repository.Suppliers).SupplierNumber.Value);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Create_WithDuplicateNormalizedNumber_ReturnsDuplicateWithoutWriting()
    {
        var repository = new FakeSupplierRepository();
        repository.Suppliers.Add(CreateSupplier("SUP-001"));
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new CreateSupplierUseCase(
            repository, unitOfWork, new FakeClock(FixedTime));

        var result = await useCase.ExecuteAsync(
            new CreateSupplierCommand("  sup-001 ", "Duplicate"));

        Assert.Equal(CreateSupplierOutcome.DuplicateSupplierNumber, result.Outcome);
        Assert.Null(result.Supplier);
        Assert.Single(repository.Suppliers);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetById_WithExistingInactiveSupplier_ReturnsMappedSupplier()
    {
        var repository = new FakeSupplierRepository();
        var supplier = CreateSupplier("SUP-002");
        supplier.Deactivate(FixedTime.AddHours(1));
        repository.Suppliers.Add(supplier);
        var useCase = new GetSupplierByIdUseCase(repository);

        var result = await useCase.ExecuteAsync(supplier.Id);

        Assert.True(result.Found);
        Assert.NotNull(result.Supplier);
        Assert.Equal(supplier.Id, result.Supplier.Id);
        Assert.Equal("SUP-002", result.Supplier.SupplierNumber);
        Assert.Equal(SupplierStatus.Inactive, result.Supplier.Status);
    }

    [Fact]
    public async Task GetById_WithMissingSupplier_ReturnsNotFound()
    {
        var useCase = new GetSupplierByIdUseCase(new FakeSupplierRepository());

        var result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.Found);
        Assert.Null(result.Supplier);
    }

    private static Supplier CreateSupplier(string supplierNumber) => new(
        Guid.NewGuid(), new SupplierNumber(supplierNumber), "Supplier", FixedTime);
}
