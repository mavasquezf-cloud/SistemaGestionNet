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
using SistemaGestion.Application.Customers.ChangeCustomerStatus;
using SistemaGestion.Application.Customers.CreateCustomer;
using SistemaGestion.Application.Customers.GetCustomerById;
using SistemaGestion.Application.Customers.GetCustomers;
using SistemaGestion.Application.Suppliers.ChangeSupplierStatus;
using SistemaGestion.Application.Suppliers.CreateSupplier;
using SistemaGestion.Application.Suppliers.GetSupplierById;
using SistemaGestion.Application.Suppliers.GetSuppliers;
using SistemaGestion.Infrastructure;
using SistemaGestion.Application.Purchasing.AddPurchaseLine;
using SistemaGestion.Application.Purchasing.CancelPurchase;
using SistemaGestion.Application.Purchasing.ConfirmPurchase;
using SistemaGestion.Application.Purchasing.CreatePurchase;
using SistemaGestion.Application.Purchasing.GetPurchaseById;
using SistemaGestion.Application.Purchasing.GetPurchases;
using SistemaGestion.Application.Purchasing.ReceivePurchase;

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
builder.Services.AddScoped<CreateCustomerUseCase>();
builder.Services.AddScoped<GetCustomerByIdUseCase>();
builder.Services.AddScoped<GetCustomersUseCase>();
builder.Services.AddScoped<ChangeCustomerStatusUseCase>();
builder.Services.AddScoped<CreateSupplierUseCase>();
builder.Services.AddScoped<GetSupplierByIdUseCase>();
builder.Services.AddScoped<GetSuppliersUseCase>();
builder.Services.AddScoped<ChangeSupplierStatusUseCase>();
builder.Services.AddScoped<CreatePurchaseUseCase>();
builder.Services.AddScoped<AddPurchaseLineUseCase>();
builder.Services.AddScoped<ConfirmPurchaseUseCase>();
builder.Services.AddScoped<CancelPurchaseUseCase>();
builder.Services.AddScoped<ReceivePurchaseUseCase>();
builder.Services.AddScoped<GetPurchaseByIdUseCase>();
builder.Services.AddScoped<GetPurchasesUseCase>();

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
app.MapCustomerEndpoints();
app.MapSupplierEndpoints();
app.MapPurchaseEndpoints();

app.Run();

public partial class Program;
