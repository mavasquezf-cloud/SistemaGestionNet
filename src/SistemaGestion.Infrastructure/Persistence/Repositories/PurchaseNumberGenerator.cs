using System.Data;
using Microsoft.EntityFrameworkCore;
using SistemaGestion.Application.Purchasing.Persistence;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Infrastructure.Persistence.Repositories;

internal sealed class PurchaseNumberGenerator(SistemaGestionDbContext dbContext) : IPurchaseNumberGenerator
{
    public async Task<PurchaseNumber> NextAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT NEXT VALUE FOR dbo.PurchaseNumberSequence";
            var value = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            return new PurchaseNumber($"PUR-{value:00000000}");
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}
