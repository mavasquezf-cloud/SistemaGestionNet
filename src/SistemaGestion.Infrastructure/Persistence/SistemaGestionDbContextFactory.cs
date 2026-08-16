using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SistemaGestion.Infrastructure.Persistence;

internal sealed class SistemaGestionDbContextFactory : IDesignTimeDbContextFactory<SistemaGestionDbContext>
{
    public SistemaGestionDbContext CreateDbContext(string[] args)
    {
        var apiDirectory = FindApiDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SistemaGestionDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SistemaGestionDb' was not configured.");

        var options = new DbContextOptionsBuilder<SistemaGestionDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SistemaGestionDbContext(options);
    }

    private static string FindApiDirectory()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "SistemaGestion.API");
            if (File.Exists(Path.Combine(candidate, "appsettings.Development.json")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the SistemaGestion.API configuration directory.");
    }
}
