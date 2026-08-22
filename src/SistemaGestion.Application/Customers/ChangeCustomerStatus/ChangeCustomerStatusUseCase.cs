using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Customers.Persistence;
using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Application.Customers.ChangeCustomerStatus;

public sealed record ChangeCustomerStatusCommand(Guid CustomerId, CustomerStatus Status);
public enum ChangeCustomerStatusOutcome { Success, CustomerNotFound, ConcurrencyConflict }
public sealed record ChangeCustomerStatusResult(ChangeCustomerStatusOutcome Outcome, CustomerResult? Customer);

public sealed class ChangeCustomerStatusUseCase(
    ICustomerRepository customers, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<ChangeCustomerStatusResult> ExecuteAsync(
        ChangeCustomerStatusCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var customer = await customers.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customer is null) return new(ChangeCustomerStatusOutcome.CustomerNotFound, null);
        switch (command.Status)
        {
            case CustomerStatus.Active: customer.Activate(clock.UtcNow); break;
            case CustomerStatus.Inactive: customer.Deactivate(clock.UtcNow); break;
            default: throw new ArgumentOutOfRangeException(nameof(command), command.Status, "Customer status is not supported.");
        }
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (CustomerConcurrencyException)
        { return new(ChangeCustomerStatusOutcome.ConcurrencyConflict, null); }
        return new(ChangeCustomerStatusOutcome.Success, CustomerResult.FromCustomer(customer));
    }
}
