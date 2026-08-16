namespace SistemaGestion.Application.Inventory.AdjustInventory;

public enum AdjustInventoryOutcome
{
    Success = 1,
    ProductNotFound = 2,
    ProductInactive = 3,
    InsufficientStock = 4,
    ConcurrencyConflict = 5
}
