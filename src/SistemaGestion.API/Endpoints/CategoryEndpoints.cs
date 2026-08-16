using Microsoft.AspNetCore.Http.HttpResults;
using SistemaGestion.API.Contracts;
using SistemaGestion.Application.Catalog.Categories.CreateCategory;
using SistemaGestion.Application.Catalog.Categories.GetCategories;

namespace SistemaGestion.API.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/categories").WithTags("Categories");

        group.MapPost("", CreateCategoryAsync)
            .WithName("CreateCategory")
            .WithSummary("Create a category")
            .WithDescription("Creates an active Catalog category.")
            .Produces<CategoryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("", GetCategoriesAsync)
            .WithName("GetCategories")
            .WithSummary("List categories")
            .WithDescription("Returns all Catalog categories ordered by name.")
            .Produces<IReadOnlyList<CategoryResponse>>();

        return endpoints;
    }

    private static async Task<Created<CategoryResponse>> CreateCategoryAsync(
        CreateCategoryRequest request,
        CreateCategoryUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new CreateCategoryCommand(request.Name, request.Description), cancellationToken);
        var response = CategoryResponse.FromResult(result);
        return TypedResults.Created("/api/categories", response);
    }

    private static async Task<Ok<IReadOnlyList<CategoryResponse>>> GetCategoriesAsync(
        GetCategoriesUseCase useCase,
        CancellationToken cancellationToken)
    {
        var results = await useCase.ExecuteAsync(cancellationToken);
        IReadOnlyList<CategoryResponse> response = results.Select(CategoryResponse.FromResult).ToArray();
        return TypedResults.Ok(response);
    }
}
