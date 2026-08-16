namespace SistemaGestion.Application.Inventory.GetInventoryByProductId;

public sealed record GetInventoryByProductIdResult(
    GetInventoryByProductIdOutcome Outcome,
    Guid ProductId,
    decimal? QuantityOnHand);
