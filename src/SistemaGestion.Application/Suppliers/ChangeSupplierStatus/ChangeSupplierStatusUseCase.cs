using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Suppliers.Persistence;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.Suppliers.ChangeSupplierStatus;

public sealed class ChangeSupplierStatusUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<ChangeSupplierStatusResult> ExecuteAsync(
        ChangeSupplierStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = await supplierRepository.GetByIdAsync(
            command.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return new ChangeSupplierStatusResult(
                ChangeSupplierStatusOutcome.SupplierNotFound, null);
        }

        switch (command.Status)
        {
            case SupplierStatus.Active:
                supplier.Activate(clock.UtcNow);
                break;
            case SupplierStatus.Inactive:
                supplier.Deactivate(clock.UtcNow);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(command), command.Status, "Supplier status is not supported.");
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (SupplierConcurrencyException)
        {
            return new ChangeSupplierStatusResult(
                ChangeSupplierStatusOutcome.ConcurrencyConflict, null);
        }

        return new ChangeSupplierStatusResult(
            ChangeSupplierStatusOutcome.Success,
            SupplierResult.FromSupplier(supplier));
    }
}
