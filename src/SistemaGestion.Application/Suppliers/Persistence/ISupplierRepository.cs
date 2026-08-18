using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.Suppliers.Persistence;

public interface ISupplierRepository
{
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);

    Task<Supplier?> GetByIdAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default);

    Task<SupplierPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsBySupplierNumberAsync(
        SupplierNumber supplierNumber,
        CancellationToken cancellationToken = default);
}
