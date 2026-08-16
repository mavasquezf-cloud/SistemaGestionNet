namespace SistemaGestion.Application.Inventory.AdjustInventory;

public sealed record AdjustInventoryResult(
    AdjustInventoryOutcome Outcome,
    decimal? QuantityOnHand,
    InventoryMovementResult? Movement);
