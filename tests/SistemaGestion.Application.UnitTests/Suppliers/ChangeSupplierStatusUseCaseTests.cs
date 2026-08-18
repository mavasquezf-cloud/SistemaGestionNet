using SistemaGestion.Application.Suppliers.ChangeSupplierStatus;
using SistemaGestion.Application.UnitTests.Suppliers.Fakes;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.UnitTests.Suppliers;

public sealed class ChangeSupplierStatusUseCaseTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ChangeTime = CreatedAt.AddHours(2);

    [Fact]
    public async Task Execute_DeactivatesActiveSupplierUsingClockAndSavesOnce()
    {
        var context = CreateContext();

        var result = await context.UseCase.ExecuteAsync(
            new ChangeSupplierStatusCommand(context.Supplier.Id, SupplierStatus.Inactive));

        Assert.Equal(ChangeSupplierStatusOutcome.Success, result.Outcome);
        Assert.Equal(SupplierStatus.Inactive, result.Supplier!.Status);
        Assert.Equal(ChangeTime, result.Supplier.UpdatedAt);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Execute_ActivatesInactiveSupplierUsingClock()
    {
        var context = CreateContext();
        context.Supplier.Deactivate(CreatedAt.AddHours(1));

        var result = await context.UseCase.ExecuteAsync(
            new ChangeSupplierStatusCommand(context.Supplier.Id, SupplierStatus.Active));

        Assert.Equal(ChangeSupplierStatusOutcome.Success, result.Outcome);
        Assert.Equal(SupplierStatus.Active, result.Supplier!.Status);
        Assert.Equal(ChangeTime, result.Supplier.UpdatedAt);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Execute_WithSameStatus_PreservesUpdatedAtAndReturnsSuccess()
    {
        var context = CreateContext();

        var result = await context.UseCase.ExecuteAsync(
            new ChangeSupplierStatusCommand(context.Supplier.Id, SupplierStatus.Active));

        Assert.Equal(ChangeSupplierStatusOutcome.Success, result.Outcome);
        Assert.Equal(CreatedAt, result.Supplier!.UpdatedAt);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Execute_WithMissingSupplier_ReturnsNotFoundWithoutSaving()
    {
        var repository = new FakeSupplierRepository();
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ChangeSupplierStatusUseCase(
            repository, unitOfWork, new FakeClock(ChangeTime));

        var result = await useCase.ExecuteAsync(
            new ChangeSupplierStatusCommand(Guid.NewGuid(), SupplierStatus.Inactive));

        Assert.Equal(ChangeSupplierStatusOutcome.SupplierNotFound, result.Outcome);
        Assert.Null(result.Supplier);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Execute_WhenSaveReportsConcurrency_ReturnsExplicitConflictOutcome()
    {
        var context = CreateContext();
        context.UnitOfWork.ThrowConcurrencyConflict = true;

        var result = await context.UseCase.ExecuteAsync(
            new ChangeSupplierStatusCommand(context.Supplier.Id, SupplierStatus.Inactive));

        Assert.Equal(ChangeSupplierStatusOutcome.ConcurrencyConflict, result.Outcome);
        Assert.Null(result.Supplier);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    private static TestContext CreateContext()
    {
        var supplier = new Supplier(
            Guid.NewGuid(), new SupplierNumber("SUP-001"), "Supplier", CreatedAt);
        var repository = new FakeSupplierRepository();
        repository.Suppliers.Add(supplier);
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ChangeSupplierStatusUseCase(
            repository, unitOfWork, new FakeClock(ChangeTime));
        return new TestContext(supplier, unitOfWork, useCase);
    }

    private sealed record TestContext(
        Supplier Supplier,
        FakeUnitOfWork UnitOfWork,
        ChangeSupplierStatusUseCase UseCase);
}
