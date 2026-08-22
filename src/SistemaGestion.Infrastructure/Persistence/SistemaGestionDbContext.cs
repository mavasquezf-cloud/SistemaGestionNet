using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Customers.Persistence;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Application.Suppliers.Persistence;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Domain.Catalog.Categories;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Customers;
using SistemaGestion.Domain.Inventory;
using SistemaGestion.Domain.Suppliers;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Infrastructure.Persistence;

public sealed class SistemaGestionDbContext(DbContextOptions<SistemaGestionDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Any(entry => entry.Entity is Purchase))
        {
            throw new PurchaseConcurrencyException(
                "Purchase was changed by another operation.", exception);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Any(entry => entry.Entity is InventoryItem))
        {
            throw new InventoryConcurrencyException(
                "Inventory was changed by another operation.", exception);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Any(entry => entry.Entity is Customer))
        {
            throw new CustomerConcurrencyException(
                "Customer was changed by another operation.", exception);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Any(entry => entry.Entity is Supplier))
        {
            throw new SupplierConcurrencyException(
                "Supplier was changed by another operation.", exception);
        }
        catch (DbUpdateException exception) when (IsInventoryItemCreationRace(exception))
        {
            throw new InventoryConcurrencyException(
                "Inventory was created by another operation.", exception);
        }
        catch (DbUpdateException exception) when (IsDuplicateSupplierNumber(exception))
        {
            throw new SupplierDuplicateNumberException(
                "A Supplier with the same SupplierNumber already exists.", exception);
        }
        catch (DbUpdateException exception) when (IsDuplicateCustomerNumber(exception))
        {
            throw new CustomerDuplicateNumberException(
                "A Customer with the same CustomerNumber already exists.", exception);
        }
        catch (DbUpdateException exception) when (IsDuplicatePurchaseNumber(exception))
        {
            throw new PurchaseDuplicateNumberException(
                "A Purchase with the same PurchaseNumber already exists.", exception);
        }
        catch (DbUpdateException exception) when (IsDuplicatePurchaseReceipt(exception))
        {
            throw new PurchaseReceiptConflictException(
                "This Purchase receipt was already applied for the Product.", exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("PurchaseNumberSequence")
            .StartsAt(1)
            .IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SistemaGestionDbContext).Assembly);
    }

    private static bool IsInventoryItemCreationRace(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
            && sqlException.Message.Contains(
                "IX_InventoryItems_ProductId", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDuplicateSupplierNumber(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
            && sqlException.Message.Contains(
                "UX_Suppliers_SupplierNumber", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDuplicateCustomerNumber(DbUpdateException exception) =>
        HasUniqueIndex(exception, "UX_Customers_CustomerNumber");

    private static bool IsDuplicatePurchaseNumber(DbUpdateException exception) =>
        HasUniqueIndex(exception, "UX_Purchases_PurchaseNumber");

    private static bool IsDuplicatePurchaseReceipt(DbUpdateException exception) =>
        HasUniqueIndex(exception, "UX_InventoryMovements_PurchaseReceipt_Product");

    private static bool HasUniqueIndex(DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
        && sqlException.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase);
}
