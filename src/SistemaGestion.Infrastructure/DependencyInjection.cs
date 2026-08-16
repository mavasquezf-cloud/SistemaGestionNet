using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Infrastructure.Persistence;
using SistemaGestion.Infrastructure.Persistence.Repositories;

namespace SistemaGestion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SistemaGestionDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SistemaGestionDb' was not configured.");

        services.AddDbContext<SistemaGestionDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<SistemaGestionDbContext>());

        return services;
    }
}
