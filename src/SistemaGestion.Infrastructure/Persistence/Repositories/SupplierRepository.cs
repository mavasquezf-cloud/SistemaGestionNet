using Microsoft.EntityFrameworkCore;
using SistemaGestion.Application.Suppliers.Persistence;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Infrastructure.Persistence.Repositories;

internal sealed class SupplierRepository(SistemaGestionDbContext dbContext) : ISupplierRepository
{
    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        await dbContext.Suppliers.AddAsync(supplier, cancellationToken);
    }

    public Task<Supplier?> GetByIdAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Suppliers.SingleOrDefaultAsync(
            supplier => supplier.Id == supplierId, cancellationToken);
    }

    public async Task<SupplierPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.Suppliers.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(supplier => supplier.SupplierNumber)
            .ThenBy(supplier => supplier.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new SupplierPage(items, totalCount);
    }

    public Task<bool> ExistsBySupplierNumberAsync(
        SupplierNumber supplierNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supplierNumber);
        return dbContext.Suppliers
            .AsNoTracking()
            .AnyAsync(
                supplier => supplier.SupplierNumber == supplierNumber,
                cancellationToken);
    }
}
