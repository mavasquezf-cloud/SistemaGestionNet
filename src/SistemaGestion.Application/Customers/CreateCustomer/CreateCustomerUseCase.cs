using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Customers.Persistence;
using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Application.Customers.CreateCustomer;

public sealed record CreateCustomerCommand(string CustomerNumber, string Name,
    string? TaxIdentificationNumber = null, string? Email = null,
    string? Phone = null, string? Address = null);
public enum CreateCustomerOutcome { Success, DuplicateCustomerNumber }
public sealed record CreateCustomerResult(CreateCustomerOutcome Outcome, CustomerResult? Customer);

public sealed class CreateCustomerUseCase(
    ICustomerRepository customers, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<CreateCustomerResult> ExecuteAsync(
        CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var customerNumber = new CustomerNumber(command.CustomerNumber);
        if (await customers.ExistsByCustomerNumberAsync(customerNumber, cancellationToken))
            return new(CreateCustomerOutcome.DuplicateCustomerNumber, null);

        var customer = new Customer(Guid.NewGuid(), customerNumber, command.Name, clock.UtcNow,
            command.TaxIdentificationNumber, command.Email, command.Phone, command.Address);
        await customers.AddAsync(customer, cancellationToken);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (CustomerDuplicateNumberException)
        { return new(CreateCustomerOutcome.DuplicateCustomerNumber, null); }
        return new(CreateCustomerOutcome.Success, CustomerResult.FromCustomer(customer));
    }
}
