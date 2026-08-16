namespace SistemaGestion.Application.Inventory.AdjustInventory;

public sealed record AdjustInventoryCommand(
    Guid ProductId,
    decimal QuantityDelta,
    string Reason,
    string? Reference = null);
