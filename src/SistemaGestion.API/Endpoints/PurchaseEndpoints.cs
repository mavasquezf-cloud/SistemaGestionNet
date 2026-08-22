using Microsoft.AspNetCore.Mvc;
using SistemaGestion.API.Contracts;
using SistemaGestion.Application.Purchasing.AddPurchaseLine;
using SistemaGestion.Application.Purchasing.CancelPurchase;
using SistemaGestion.Application.Purchasing.ConfirmPurchase;
using SistemaGestion.Application.Purchasing.CreatePurchase;
using SistemaGestion.Application.Purchasing.GetPurchaseById;
using SistemaGestion.Application.Purchasing.GetPurchases;
using SistemaGestion.Application.Purchasing.ReceivePurchase;

namespace SistemaGestion.API.Endpoints;

public static class PurchaseEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/purchases").WithTags("Purchases");
        group.MapPost("", CreateAsync).WithName("CreatePurchase").WithSummary("Create a purchase")
            .WithDescription("Creates a draft purchase using a generated purchase number and supplier snapshot.")
            .Produces<PurchaseResponse>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesValidationProblem();
        group.MapPost("/{id:guid}/lines", AddLineAsync).WithName("AddPurchaseLine").WithSummary("Add a purchase line")
            .WithDescription("Adds an active product snapshot to a draft purchase.")
            .Produces<PurchaseResponse>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesValidationProblem();
        group.MapGet("", GetAllAsync).WithName("GetPurchases").WithSummary("List purchases")
            .WithDescription("Returns purchases of every status with SQL-backed pagination.").Produces<PagedPurchasesResponse>();
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetPurchaseById").WithSummary("Get a purchase")
            .WithDescription("Returns a complete purchase including historical line snapshots.").Produces<PurchaseResponse>().ProducesProblem(404);
        group.MapPost("/{id:guid}/confirm", ConfirmAsync).WithName("ConfirmPurchase").WithSummary("Confirm a purchase")
            .WithDescription("Confirms a non-empty draft purchase.").Produces<PurchaseResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/{id:guid}/receive", ReceiveAsync).WithName("ReceivePurchase").WithSummary("Receive a purchase")
            .WithDescription("Atomically receives a confirmed purchase and increases inventory.").Produces<PurchaseResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/{id:guid}/cancel", CancelAsync).WithName("CancelPurchase").WithSummary("Cancel a purchase")
            .WithDescription("Cancels a draft or confirmed purchase without changing inventory.").Produces<PurchaseResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreatePurchaseRequest request, CreatePurchaseUseCase useCase, CancellationToken ct)
    {
        if (request.SupplierId == Guid.Empty)
            return BadRequest("Invalid supplier identifier", "SupplierId must be a non-empty GUID.");
        var result = await useCase.ExecuteAsync(new(request.SupplierId, request.SupplierDocumentReference), ct);
        return result.Outcome switch
        {
            CreatePurchaseOutcome.Success => TypedResults.Created($"/api/purchases/{result.Purchase!.Id}", PurchaseResponse.FromResult(result.Purchase)),
            CreatePurchaseOutcome.SupplierNotFound => NotFound("Supplier not found", $"Supplier '{request.SupplierId}' does not exist."),
            CreatePurchaseOutcome.SupplierInactive => BadRequest("Supplier is inactive", "A purchase can only be created for an active supplier."),
            CreatePurchaseOutcome.DuplicatePurchaseNumber => Conflict("Duplicate purchase number", "The generated purchase number already exists."),
            _ => throw Unsupported(result.Outcome)
        };
    }

    private static async Task<IResult> AddLineAsync(Guid id, AddPurchaseLineRequest request, AddPurchaseLineUseCase useCase, CancellationToken ct)
    {
        if (request.ProductId == Guid.Empty)
            return BadRequest("Invalid product identifier", "ProductId must be a non-empty GUID.");
        var result = await useCase.ExecuteAsync(new(id, request.ProductId, request.Quantity, request.UnitCost), ct);
        return result.Outcome switch
        {
            AddPurchaseLineOutcome.Success => TypedResults.Created($"/api/purchases/{id}", PurchaseResponse.FromResult(result.Purchase!)),
            AddPurchaseLineOutcome.PurchaseNotFound => NotFound("Purchase not found", $"Purchase '{id}' does not exist."),
            AddPurchaseLineOutcome.PurchaseNotDraft => BadRequest("Purchase is not draft", "Lines can only be added to a draft purchase."),
            AddPurchaseLineOutcome.ProductNotFound => NotFound("Product not found", $"Product '{request.ProductId}' does not exist."),
            AddPurchaseLineOutcome.ProductInactive => BadRequest("Product is inactive", "An inactive product cannot be added to a purchase."),
            AddPurchaseLineOutcome.DuplicateProduct => Conflict("Duplicate product", "The product already exists in this purchase."),
            AddPurchaseLineOutcome.ConcurrencyConflict => Conflict("Purchase concurrency conflict", "The purchase changed during this operation."),
            _ => throw Unsupported(result.Outcome)
        };
    }

    private static async Task<IResult> ConfirmAsync(Guid id, ConfirmPurchaseUseCase useCase, CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(id, ct);
        return result.Outcome switch
        {
            ConfirmPurchaseOutcome.Success => TypedResults.Ok(PurchaseResponse.FromResult(result.Purchase!)),
            ConfirmPurchaseOutcome.PurchaseNotFound => NotFound("Purchase not found", $"Purchase '{id}' does not exist."),
            ConfirmPurchaseOutcome.EmptyPurchase => BadRequest("Empty purchase", "A purchase must contain a line before confirmation."),
            ConfirmPurchaseOutcome.InvalidStatus => BadRequest("Invalid purchase status", "Only a draft purchase can be confirmed."),
            ConfirmPurchaseOutcome.ConcurrencyConflict => Conflict("Purchase concurrency conflict", "The purchase changed during this operation."),
            _ => throw Unsupported(result.Outcome)
        };
    }

    private static async Task<IResult> CancelAsync(Guid id, CancelPurchaseUseCase useCase, CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(id, ct);
        return result.Outcome switch
        {
            CancelPurchaseOutcome.Success => TypedResults.Ok(PurchaseResponse.FromResult(result.Purchase!)),
            CancelPurchaseOutcome.PurchaseNotFound => NotFound("Purchase not found", $"Purchase '{id}' does not exist."),
            CancelPurchaseOutcome.InvalidStatus => BadRequest("Invalid purchase status", "Only a draft or confirmed purchase can be cancelled."),
            CancelPurchaseOutcome.ConcurrencyConflict => Conflict("Purchase concurrency conflict", "The purchase changed during this operation."),
            _ => throw Unsupported(result.Outcome)
        };
    }

    private static async Task<IResult> ReceiveAsync(Guid id, ReceivePurchaseUseCase useCase, CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(id, ct);
        return result.Outcome switch
        {
            ReceivePurchaseOutcome.Success => TypedResults.Ok(PurchaseResponse.FromResult(result.Purchase!)),
            ReceivePurchaseOutcome.PurchaseNotFound => NotFound("Purchase not found", $"Purchase '{id}' does not exist."),
            ReceivePurchaseOutcome.PurchaseNotConfirmed => BadRequest("Purchase is not confirmed", "Only a confirmed purchase can be received."),
            ReceivePurchaseOutcome.AlreadyReceived => Conflict("Purchase already received", "The purchase receipt was already applied."),
            ReceivePurchaseOutcome.ConcurrencyConflict => Conflict("Purchase receipt conflict", "The purchase or inventory changed during receipt."),
            _ => throw Unsupported(result.Outcome)
        };
    }

    private static async Task<IResult> GetByIdAsync(Guid id, GetPurchaseByIdUseCase useCase, CancellationToken ct)
    {
        var result = await useCase.ExecuteAsync(id, ct);
        return result.Outcome == GetPurchaseByIdOutcome.Found
            ? TypedResults.Ok(PurchaseResponse.FromResult(result.Purchase!))
            : NotFound("Purchase not found", $"Purchase '{id}' does not exist.");
    }

    private static async Task<IResult> GetAllAsync(GetPurchasesUseCase useCase, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await useCase.ExecuteAsync(new(page, pageSize), ct);
        return TypedResults.Ok(new PagedPurchasesResponse(result.Items.Select(PurchaseResponse.FromResult).ToArray(), result.Page, result.PageSize, result.TotalCount));
    }

    private static IResult BadRequest(string title, string detail) => TypedResults.BadRequest(Problem(400, title, detail));
    private static IResult NotFound(string title, string detail) => TypedResults.NotFound(Problem(404, title, detail));
    private static IResult Conflict(string title, string detail) => TypedResults.Conflict(Problem(409, title, detail));
    private static ProblemDetails Problem(int status, string title, string detail) => new() { Status = status, Title = title, Detail = detail };
    private static InvalidOperationException Unsupported<T>(T outcome) => new($"Unsupported Purchasing outcome: {outcome}.");
}
