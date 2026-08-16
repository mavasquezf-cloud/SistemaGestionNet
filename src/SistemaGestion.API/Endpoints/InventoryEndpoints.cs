using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SistemaGestion.API.Contracts;
using SistemaGestion.Application.Inventory.AdjustInventory;
using SistemaGestion.Application.Inventory.GetInventoryByProductId;
using SistemaGestion.Application.Inventory.GetInventoryMovements;

namespace SistemaGestion.API.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/inventory").WithTags("Inventory");

        group.MapPost("/{productId:guid}/adjustments", AdjustInventoryAsync)
            .WithName("AdjustInventory")
            .WithSummary("Adjust product inventory")
            .WithDescription(
                "Applies a signed manual adjustment and records an immutable inventory movement.")
            .Produces<InventoryAdjustmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapGet("/{productId:guid}", GetInventoryAsync)
            .WithName("GetInventory")
            .WithSummary("Get product inventory")
            .WithDescription("Returns the current on-hand quantity, including zero before the first adjustment.")
            .Produces<InventoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{productId:guid}/movements", GetInventoryMovementsAsync)
            .WithName("GetInventoryMovements")
            .WithSummary("Get inventory movement history")
            .WithDescription("Returns immutable inventory movements newest first with SQL-backed pagination.")
            .Produces<PagedInventoryMovementsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<
        Created<InventoryAdjustmentResponse>,
        BadRequest<ProblemDetails>,
        NotFound<ProblemDetails>,
        Conflict<ProblemDetails>>> AdjustInventoryAsync(
        Guid productId,
        ManualInventoryAdjustmentRequest request,
        AdjustInventoryUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new AdjustInventoryCommand(
                productId, request.QuantityDelta, request.Reason, request.Reference),
            cancellationToken);

        return result.Outcome switch
        {
            AdjustInventoryOutcome.Success => TypedResults.Created(
                $"/api/inventory/{productId}/movements",
                ToAdjustmentResponse(productId, result)),
            AdjustInventoryOutcome.ProductNotFound => TypedResults.NotFound(Problem(
                StatusCodes.Status404NotFound,
                "Product not found",
                $"Product '{productId}' does not exist.")),
            AdjustInventoryOutcome.ProductInactive => TypedResults.BadRequest(Problem(
                StatusCodes.Status400BadRequest,
                "Product is inactive",
                "Manual inventory adjustments require an active product.")),
            AdjustInventoryOutcome.InsufficientStock => TypedResults.BadRequest(Problem(
                StatusCodes.Status400BadRequest,
                "Insufficient stock",
                "The adjustment would make quantity on hand negative.")),
            AdjustInventoryOutcome.ConcurrencyConflict => TypedResults.Conflict(Problem(
                StatusCodes.Status409Conflict,
                "Inventory concurrency conflict",
                "Inventory changed during this adjustment. Reload it and retry.")),
            _ => throw new InvalidOperationException(
                $"Unsupported inventory adjustment outcome: {result.Outcome}.")
        };
    }

    private static async Task<Results<Ok<InventoryResponse>, NotFound<ProblemDetails>>> GetInventoryAsync(
        Guid productId,
        GetInventoryByProductIdUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(productId, cancellationToken);
        return result.Outcome == GetInventoryByProductIdOutcome.Found
            ? TypedResults.Ok(new InventoryResponse(productId, result.QuantityOnHand!.Value))
            : TypedResults.NotFound(Problem(
                StatusCodes.Status404NotFound,
                "Product not found",
                $"Product '{productId}' does not exist."));
    }

    private static async Task<Results<Ok<PagedInventoryMovementsResponse>, NotFound<ProblemDetails>>>
        GetInventoryMovementsAsync(
            Guid productId,
            GetInventoryMovementsUseCase useCase,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
    {
        var result = await useCase.ExecuteAsync(
            new GetInventoryMovementsQuery(productId, page, pageSize), cancellationToken);
        if (result.Outcome == GetInventoryMovementsOutcome.ProductNotFound)
        {
            return TypedResults.NotFound(Problem(
                StatusCodes.Status404NotFound,
                "Product not found",
                $"Product '{productId}' does not exist."));
        }

        return TypedResults.Ok(new PagedInventoryMovementsResponse(
            result.Items.Select(InventoryMovementResponse.FromResult).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static InventoryAdjustmentResponse ToAdjustmentResponse(
        Guid productId,
        AdjustInventoryResult result)
    {
        var movement = result.Movement!;
        return new InventoryAdjustmentResponse(
            productId,
            movement.QuantityDelta,
            result.QuantityOnHand!.Value,
            movement.Id,
            movement.Type.ToString(),
            movement.Source.ToString(),
            movement.Reference,
            movement.Reason,
            movement.OccurredAt);
    }

    private static ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };
}
