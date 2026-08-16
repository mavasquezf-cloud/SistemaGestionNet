using Microsoft.EntityFrameworkCore;
using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Domain.Catalog.Categories;
using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Infrastructure.Persistence;

public sealed class SistemaGestionDbContext(DbContextOptions<SistemaGestionDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SistemaGestionDbContext).Assembly);
    }
}
