using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Inventory.Persistence;
using SistemaGestion.Domain.Catalog.Categories;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Infrastructure.Persistence;

public sealed class SistemaGestionDbContext(DbContextOptions<SistemaGestionDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Any(entry => entry.Entity is InventoryItem))
        {
            throw new InventoryConcurrencyException(
                "Inventory was changed by another operation.", exception);
        }
        catch (DbUpdateException exception) when (IsInventoryItemCreationRace(exception))
        {
            throw new InventoryConcurrencyException(
                "Inventory was created by another operation.", exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SistemaGestionDbContext).Assembly);
    }

    private static bool IsInventoryItemCreationRace(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
            && sqlException.Message.Contains(
                "IX_InventoryItems_ProductId", StringComparison.OrdinalIgnoreCase);
    }
}
