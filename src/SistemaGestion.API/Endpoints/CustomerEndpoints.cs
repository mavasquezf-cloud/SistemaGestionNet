using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SistemaGestion.API.Contracts;
using SistemaGestion.Application.Customers.ChangeCustomerStatus;
using SistemaGestion.Application.Customers.CreateCustomer;
using SistemaGestion.Application.Customers.GetCustomerById;
using SistemaGestion.Application.Customers.GetCustomers;
using SistemaGestion.Domain.Customers;

namespace SistemaGestion.API.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/customers").WithTags("Customers");

        group.MapPost("", CreateCustomerAsync)
            .WithName("CreateCustomer")
            .WithSummary("Create a customer")
            .WithDescription("Creates an active customer with a normalized unique customer number.")
            .Produces<CustomerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapGet("", GetCustomersAsync)
            .WithName("GetCustomers")
            .WithSummary("List customers")
            .WithDescription("Returns active and inactive customers with SQL-backed pagination.")
            .Produces<PagedCustomersResponse>();

        group.MapGet("/{id:guid}", GetCustomerByIdAsync)
            .WithName("GetCustomerById")
            .WithSummary("Get a customer")
            .WithDescription("Returns an active or inactive customer by identifier.")
            .Produces<CustomerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/status", ChangeCustomerStatusAsync)
            .WithName("ChangeCustomerStatus")
            .WithSummary("Change customer status")
            .WithDescription("Activates or deactivates a customer through explicit lifecycle behavior.")
            .Produces<CustomerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<Results<Created<CustomerResponse>, Conflict<ProblemDetails>>>
        CreateCustomerAsync(
            CreateCustomerRequest request,
            CreateCustomerUseCase useCase,
            CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new CreateCustomerCommand(
                request.CustomerNumber,
                request.Name,
                request.TaxIdentificationNumber,
                request.Email,
                request.Phone,
                request.Address),
            cancellationToken);

        return result.Outcome switch
        {
            CreateCustomerOutcome.Success => TypedResults.Created(
                $"/api/customers/{result.Customer!.Id}",
                CustomerResponse.FromResult(result.Customer)),
            CreateCustomerOutcome.DuplicateCustomerNumber => TypedResults.Conflict(Problem(
                StatusCodes.Status409Conflict,
                "Duplicate customer number",
                "A customer with the normalized customer number already exists.")),
            _ => throw new InvalidOperationException(
                $"Unsupported create customer outcome: {result.Outcome}.")
        };
    }

    private static async Task<Ok<PagedCustomersResponse>> GetCustomersAsync(
        GetCustomersUseCase useCase,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await useCase.ExecuteAsync(
            new GetCustomersQuery(page, pageSize), cancellationToken);
        return TypedResults.Ok(new PagedCustomersResponse(
            result.Items.Select(CustomerResponse.FromResult).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static async Task<Results<Ok<CustomerResponse>, NotFound<ProblemDetails>>>
        GetCustomerByIdAsync(
            Guid id,
            GetCustomerByIdUseCase useCase,
            CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new GetCustomerByIdQuery(id), cancellationToken);
        return result.Outcome switch
        {
            GetCustomerByIdOutcome.Found =>
                TypedResults.Ok(CustomerResponse.FromResult(result.Customer!)),
            GetCustomerByIdOutcome.NotFound => TypedResults.NotFound(Problem(
                StatusCodes.Status404NotFound,
                "Customer not found",
                $"Customer '{id}' does not exist.")),
            _ => throw new InvalidOperationException(
                $"Unsupported get customer outcome: {result.Outcome}.")
        };
    }

    private static async Task<Results<
        Ok<CustomerResponse>,
        NotFound<ProblemDetails>,
        Conflict<ProblemDetails>>> ChangeCustomerStatusAsync(
        Guid id,
        ChangeCustomerStatusRequest request,
        ChangeCustomerStatusUseCase useCase,
        CancellationToken cancellationToken)
    {
        var targetStatus = Enum.Parse<CustomerStatus>(request.Status, ignoreCase: false);
        var result = await useCase.ExecuteAsync(
            new ChangeCustomerStatusCommand(id, targetStatus), cancellationToken);

        return result.Outcome switch
        {
            ChangeCustomerStatusOutcome.Success =>
                TypedResults.Ok(CustomerResponse.FromResult(result.Customer!)),
            ChangeCustomerStatusOutcome.CustomerNotFound => TypedResults.NotFound(Problem(
                StatusCodes.Status404NotFound,
                "Customer not found",
                $"Customer '{id}' does not exist.")),
            ChangeCustomerStatusOutcome.ConcurrencyConflict => TypedResults.Conflict(Problem(
                StatusCodes.Status409Conflict,
                "Customer concurrency conflict",
                "The customer changed during this operation. Reload it and retry.")),
            _ => throw new InvalidOperationException(
                $"Unsupported customer status outcome: {result.Outcome}.")
        };
    }

    private static ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };
}
