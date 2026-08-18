using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Suppliers.Persistence;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.Suppliers.CreateSupplier;

public sealed class CreateSupplierUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<CreateSupplierResult> ExecuteAsync(
        CreateSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplierNumber = new SupplierNumber(command.SupplierNumber);
        if (await supplierRepository.ExistsBySupplierNumberAsync(
                supplierNumber, cancellationToken))
        {
            return new CreateSupplierResult(
                CreateSupplierOutcome.DuplicateSupplierNumber, null);
        }

        var supplier = new Supplier(
            Guid.NewGuid(),
            supplierNumber,
            command.Name,
            clock.UtcNow,
            command.TaxIdentificationNumber,
            command.Email,
            command.Phone,
            command.Address);

        await supplierRepository.AddAsync(supplier, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (SupplierDuplicateNumberException)
        {
            return new CreateSupplierResult(
                CreateSupplierOutcome.DuplicateSupplierNumber, null);
        }

        return new CreateSupplierResult(
            CreateSupplierOutcome.Success,
            SupplierResult.FromSupplier(supplier));
    }
}
