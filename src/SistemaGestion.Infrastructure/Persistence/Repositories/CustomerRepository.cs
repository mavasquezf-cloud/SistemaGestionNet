using Microsoft.EntityFrameworkCore;
using SistemaGestion.Application.Customers.Persistence;
using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(SistemaGestionDbContext dbContext) : ICustomerRepository
{
    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return dbContext.Customers.SingleOrDefaultAsync(
            customer => customer.Id == customerId, cancellationToken);
    }

    public async Task<CustomerPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.Customers.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(customer => customer.CreatedAt)
            .ThenBy(customer => customer.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new CustomerPage(items, totalCount);
    }

    public Task<bool> ExistsByCustomerNumberAsync(
        CustomerNumber customerNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customerNumber);
        return dbContext.Customers
            .AsNoTracking()
            .AnyAsync(customer => customer.CustomerNumber == customerNumber, cancellationToken);
    }
}
