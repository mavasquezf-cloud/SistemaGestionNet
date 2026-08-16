using Scalar.AspNetCore;
using SistemaGestion.API.Endpoints;
using SistemaGestion.Application.Catalog.Categories.CreateCategory;
using SistemaGestion.Application.Catalog.Categories.GetCategories;
using SistemaGestion.Application.Catalog.Products.CreateProduct;
using SistemaGestion.Application.Catalog.Products.GetProductById;
using SistemaGestion.Application.Catalog.Products.GetProducts;
using SistemaGestion.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CreateCategoryUseCase>();
builder.Services.AddScoped<GetCategoriesUseCase>();
builder.Services.AddScoped<CreateProductUseCase>();
builder.Services.AddScoped<GetProductsUseCase>();
builder.Services.AddScoped<GetProductByIdUseCase>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapCategoryEndpoints();
app.MapProductEndpoints();

app.Run();

public partial class Program;
