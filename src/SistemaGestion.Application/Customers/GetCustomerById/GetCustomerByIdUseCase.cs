using SistemaGestion.Application.Customers.Persistence;

namespace SistemaGestion.Application.Customers.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid CustomerId);
public enum GetCustomerByIdOutcome { Found, NotFound }
public sealed record GetCustomerByIdResult(GetCustomerByIdOutcome Outcome, CustomerResult? Customer);

public sealed class GetCustomerByIdUseCase(ICustomerRepository customers)
{
    public async Task<GetCustomerByIdResult> ExecuteAsync(
        GetCustomerByIdQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var customer = await customers.GetByIdAsync(query.CustomerId, cancellationToken);
        return customer is null
            ? new(GetCustomerByIdOutcome.NotFound, null)
            : new(GetCustomerByIdOutcome.Found, CustomerResult.FromCustomer(customer));
    }
}
