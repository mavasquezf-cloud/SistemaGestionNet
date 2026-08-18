using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SistemaGestion.API.Contracts;
using SistemaGestion.Application.Suppliers.ChangeSupplierStatus;
using SistemaGestion.Application.Suppliers.CreateSupplier;
using SistemaGestion.Application.Suppliers.GetSupplierById;
using SistemaGestion.Application.Suppliers.GetSuppliers;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.API.Endpoints;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/suppliers").WithTags("Suppliers");

        group.MapPost("", CreateSupplierAsync)
            .WithName("CreateSupplier")
            .WithSummary("Create a supplier")
            .WithDescription("Creates an active supplier with a normalized unique supplier number.")
            .Produces<SupplierResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapGet("", GetSuppliersAsync)
            .WithName("GetSuppliers")
            .WithSummary("List suppliers")
            .WithDescription("Returns active and inactive suppliers with SQL-backed pagination.")
            .Produces<PagedSuppliersResponse>();

        group.MapGet("/{id:guid}", GetSupplierByIdAsync)
            .WithName("GetSupplierById")
            .WithSummary("Get a supplier")
            .WithDescription("Returns an active or inactive supplier by identifier.")
            .Produces<SupplierResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/status", ChangeSupplierStatusAsync)
            .WithName("ChangeSupplierStatus")
            .WithSummary("Change supplier status")
            .WithDescription("Activates or deactivates a supplier through explicit lifecycle behavior.")
            .Produces<SupplierResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<Results<Created<SupplierResponse>, Conflict<ProblemDetails>>>
        CreateSupplierAsync(
            CreateSupplierRequest request,
            CreateSupplierUseCase useCase,
            CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new CreateSupplierCommand(
                request.SupplierNumber,
                request.Name,
                request.TaxIdentificationNumber,
                request.Email,
                request.Phone,
                request.Address),
            cancellationToken);

        return result.Outcome switch
        {
            CreateSupplierOutcome.Success => TypedResults.Created(
                $"/api/suppliers/{result.Supplier!.Id}",
                SupplierResponse.FromResult(result.Supplier)),
            CreateSupplierOutcome.DuplicateSupplierNumber => TypedResults.Conflict(Problem(
                StatusCodes.Status409Conflict,
                "Duplicate supplier number",
                "A supplier with the normalized supplier number already exists.")),
            _ => throw new InvalidOperationException(
                $"Unsupported create supplier outcome: {result.Outcome}.")
        };
    }

    private static async Task<Ok<PagedSuppliersResponse>> GetSuppliersAsync(
        GetSuppliersUseCase useCase,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await useCase.ExecuteAsync(
            new GetSuppliersQuery(page, pageSize), cancellationToken);
        return TypedResults.Ok(new PagedSuppliersResponse(
            result.Items.Select(SupplierResponse.FromResult).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static async Task<Results<Ok<SupplierResponse>, NotFound<ProblemDetails>>>
        GetSupplierByIdAsync(
            Guid id,
            GetSupplierByIdUseCase useCase,
            CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(id, cancellationToken);
        return result.Found
            ? TypedResults.Ok(SupplierResponse.FromResult(result.Supplier!))
            : TypedResults.NotFound(Problem(
                StatusCodes.Status404NotFound,
                "Supplier not found",
                $"Supplier '{id}' does not exist."));
    }

    private static async Task<Results<
        Ok<SupplierResponse>,
        NotFound<ProblemDetails>,
        Conflict<ProblemDetails>>> ChangeSupplierStatusAsync(
        Guid id,
        ChangeSupplierStatusRequest request,
        ChangeSupplierStatusUseCase useCase,
        CancellationToken cancellationToken)
    {
        var targetStatus = Enum.Parse<SupplierStatus>(request.Status, ignoreCase: false);
        var result = await useCase.ExecuteAsync(
            new ChangeSupplierStatusCommand(id, targetStatus), cancellationToken);

        return result.Outcome switch
        {
            ChangeSupplierStatusOutcome.Success =>
                TypedResults.Ok(SupplierResponse.FromResult(result.Supplier!)),
            ChangeSupplierStatusOutcome.SupplierNotFound => TypedResults.NotFound(Problem(
                StatusCodes.Status404NotFound,
                "Supplier not found",
                $"Supplier '{id}' does not exist.")),
            ChangeSupplierStatusOutcome.ConcurrencyConflict => TypedResults.Conflict(Problem(
                StatusCodes.Status409Conflict,
                "Supplier concurrency conflict",
                "The supplier changed during this operation. Reload it and retry.")),
            _ => throw new InvalidOperationException(
                $"Unsupported supplier status outcome: {result.Outcome}.")
        };
    }

    private static ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };
}
