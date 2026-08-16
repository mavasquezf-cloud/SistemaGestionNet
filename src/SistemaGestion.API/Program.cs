using Scalar.AspNetCore;
using SistemaGestion.API.Endpoints;
using SistemaGestion.Application.Catalog.Categories.CreateCategory;
using SistemaGestion.Application.Catalog.Categories.GetCategories;
using SistemaGestion.Application.Catalog.Products.CreateProduct;
using SistemaGestion.Application.Catalog.Products.GetProductById;
using SistemaGestion.Application.Catalog.Products.GetProducts;
using SistemaGestion.Application.Inventory.AdjustInventory;
using SistemaGestion.Application.Inventory.GetInventoryByProductId;
using SistemaGestion.Application.Inventory.GetInventoryMovements;
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
builder.Services.AddScoped<AdjustInventoryUseCase>();
builder.Services.AddScoped<GetInventoryByProductIdUseCase>();
builder.Services.AddScoped<GetInventoryMovementsUseCase>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapCategoryEndpoints();
app.MapProductEndpoints();
app.MapInventoryEndpoints();

app.Run();

public partial class Program;
