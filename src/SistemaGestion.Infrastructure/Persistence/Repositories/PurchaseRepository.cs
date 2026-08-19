using Microsoft.EntityFrameworkCore;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Infrastructure.Persistence.Repositories;

internal sealed class PurchaseRepository(SistemaGestionDbContext dbContext) : IPurchaseRepository
{
    public async Task AddAsync(Purchase purchase, CancellationToken cancellationToken = default) =>
        await dbContext.Purchases.AddAsync(purchase, cancellationToken);

    public Task<Purchase?> GetByIdAsync(Guid purchaseId, CancellationToken cancellationToken = default) =>
        dbContext.Purchases.Include(purchase => purchase.Lines)
            .SingleOrDefaultAsync(purchase => purchase.Id == purchaseId, cancellationToken);

    public async Task<PurchasePage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        var query = dbContext.Purchases.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Include(purchase => purchase.Lines)
            .OrderByDescending(purchase => purchase.CreatedAt).ThenBy(purchase => purchase.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync(cancellationToken);
        return new PurchasePage(items, totalCount);
    }

    public Task<bool> ExistsByPurchaseNumberAsync(PurchaseNumber purchaseNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(purchaseNumber);
        return dbContext.Purchases.AsNoTracking()
            .AnyAsync(purchase => purchase.PurchaseNumber == purchaseNumber, cancellationToken);
    }
}
