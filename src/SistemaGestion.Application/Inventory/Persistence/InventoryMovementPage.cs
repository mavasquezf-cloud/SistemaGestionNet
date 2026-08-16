using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Application.Inventory.Persistence;

public sealed record InventoryMovementPage(
    IReadOnlyList<InventoryMovement> Items,
    int TotalCount);
