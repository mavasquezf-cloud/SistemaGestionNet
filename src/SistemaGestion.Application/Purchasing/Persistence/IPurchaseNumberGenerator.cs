using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Application.Purchasing.Persistence;

public interface IPurchaseNumberGenerator
{
    Task<PurchaseNumber> NextAsync(CancellationToken cancellationToken = default);
}
