namespace SistemaGestion.Application.Inventory.GetInventoryMovements;

public sealed record GetInventoryMovementsQuery(
    Guid ProductId,
    int Page = 1,
    int PageSize = 50);
