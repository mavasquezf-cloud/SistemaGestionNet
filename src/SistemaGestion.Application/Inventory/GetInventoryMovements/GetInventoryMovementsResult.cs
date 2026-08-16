namespace SistemaGestion.Application.Inventory.GetInventoryMovements;

public sealed record GetInventoryMovementsResult(
    GetInventoryMovementsOutcome Outcome,
    IReadOnlyList<InventoryMovementResult> Items,
    int Page,
    int PageSize,
    int TotalCount);
