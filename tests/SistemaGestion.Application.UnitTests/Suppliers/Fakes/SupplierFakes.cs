using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Suppliers.Persistence;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.UnitTests.Suppliers.Fakes;

internal sealed class FakeSupplierRepository : ISupplierRepository
{
    public List<Supplier> Suppliers { get; } = [];

    public int AddCallCount { get; private set; }

    public int? RequestedPage { get; private set; }

    public int? RequestedPageSize { get; private set; }

    public Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        Suppliers.Add(supplier);
        return Task.CompletedTask;
    }

    public Task<Supplier?> GetByIdAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppliers.SingleOrDefault(supplier => supplier.Id == supplierId));

    public Task<SupplierPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        RequestedPage = page;
        RequestedPageSize = pageSize;
        var ordered = Suppliers
            .OrderBy(supplier => supplier.SupplierNumber.Value, StringComparer.Ordinal)
            .ThenBy(supplier => supplier.Id)
            .ToArray();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return Task.FromResult(new SupplierPage(items, ordered.Length));
    }

    public Task<bool> ExistsBySupplierNumberAsync(
        SupplierNumber supplierNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppliers.Any(supplier => supplier.SupplierNumber == supplierNumber));
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public bool ThrowConcurrencyConflict { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        if (ThrowConcurrencyConflict)
        {
            throw new SupplierConcurrencyException("Supplier was changed concurrently.");
        }

        return Task.FromResult(1);
    }
}

internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
