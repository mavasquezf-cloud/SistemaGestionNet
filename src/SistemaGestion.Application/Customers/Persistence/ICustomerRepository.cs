using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Application.Customers.Persistence;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CustomerPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCustomerNumberAsync(CustomerNumber customerNumber, CancellationToken cancellationToken = default);
}
