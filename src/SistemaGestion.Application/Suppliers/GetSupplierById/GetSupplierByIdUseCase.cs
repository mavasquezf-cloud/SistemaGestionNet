using SistemaGestion.Application.Suppliers.Persistence;

namespace SistemaGestion.Application.Suppliers.GetSupplierById;

public sealed class GetSupplierByIdUseCase(ISupplierRepository supplierRepository)
{
    public async Task<GetSupplierByIdResult> ExecuteAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        var supplier = await supplierRepository.GetByIdAsync(supplierId, cancellationToken);
        return supplier is null
            ? new GetSupplierByIdResult(false, null)
            : new GetSupplierByIdResult(true, SupplierResult.FromSupplier(supplier));
    }
}
