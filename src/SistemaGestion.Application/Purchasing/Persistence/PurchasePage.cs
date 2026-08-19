using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Application.Purchasing.Persistence;

public sealed record PurchasePage(IReadOnlyCollection<Purchase> Items, int TotalCount);
