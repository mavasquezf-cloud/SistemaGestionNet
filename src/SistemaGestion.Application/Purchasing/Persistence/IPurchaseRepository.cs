using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Application.Purchasing.Persistence;

public interface IPurchaseRepository
{
    Task AddAsync(Purchase purchase, CancellationToken cancellationToken = default);
    Task<Purchase?> GetByIdAsync(Guid purchaseId, CancellationToken cancellationToken = default);
    Task<PurchasePage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPurchaseNumberAsync(PurchaseNumber purchaseNumber, CancellationToken cancellationToken = default);
}
