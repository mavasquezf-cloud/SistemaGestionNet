using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SistemaGestion.API.Contracts;
using SistemaGestion.Application.Catalog.Products.CreateProduct;
using SistemaGestion.Application.Catalog.Products.GetProductById;
using SistemaGestion.Application.Catalog.Products.GetProducts;

namespace SistemaGestion.API.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/products").WithTags("Products");

        group.MapPost("", CreateProductAsync)
            .WithName("CreateProduct")
            .WithSummary("Create a product")
            .WithDescription("Creates a Catalog product after validating its category and normalized SKU.")
            .Produces<ProductResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapGet("", GetProductsAsync)
            .WithName("GetProducts")
            .WithSummary("List products")
            .WithDescription("Returns a page of products with category names. Page size is capped at 100.")
            .Produces<PagedProductsResponse>();

        group.MapGet("/{id:guid}", GetProductByIdAsync)
            .WithName("GetProductById")
            .WithSummary("Get a product")
            .WithDescription("Returns one product and its category name.")
            .Produces<ProductDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Created<ProductResponse>, BadRequest<ProblemDetails>, Conflict<ProblemDetails>>>
        CreateProductAsync(
            CreateProductRequest request,
            CreateProductUseCase useCase,
            CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new CreateProductCommand(
                request.Sku, request.Name, request.CategoryId, request.UnitOfMeasure,
                request.DefaultSalePrice, request.Description),
            cancellationToken);

        return result.Outcome switch
        {
            CreateProductOutcome.Success => TypedResults.Created(
                $"/api/products/{result.Product!.Id}", ProductResponse.FromResult(result.Product)),
            CreateProductOutcome.CategoryNotFound => TypedResults.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Category not found",
                Detail = $"Category '{request.CategoryId}' does not exist."
            }),
            CreateProductOutcome.DuplicateSku => TypedResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate SKU",
                Detail = $"A product with SKU '{request.Sku.Trim().ToUpperInvariant()}' already exists."
            }),
            _ => throw new InvalidOperationException($"Unsupported product outcome: {result.Outcome}.")
        };
    }

    private static async Task<Ok<PagedProductsResponse>> GetProductsAsync(
        GetProductsUseCase useCase,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await useCase.ExecuteAsync(new GetProductsQuery(page, pageSize), cancellationToken);
        var response = new PagedProductsResponse(
            result.Items.Select(ProductDetailResponse.FromResult).ToArray(),
            result.Page, result.PageSize, result.TotalCount);
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ProductDetailResponse>, NotFound<ProblemDetails>>> GetProductByIdAsync(
        Guid id,
        GetProductByIdUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(id, cancellationToken);
        return result.Found
            ? TypedResults.Ok(ProductDetailResponse.FromResult(result.Product!))
            : TypedResults.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product not found",
                Detail = $"Product '{id}' does not exist."
            });
    }
}
