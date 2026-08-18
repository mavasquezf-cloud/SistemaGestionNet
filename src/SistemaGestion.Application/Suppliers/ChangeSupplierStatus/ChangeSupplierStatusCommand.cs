using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Application.Suppliers.ChangeSupplierStatus;

public sealed record ChangeSupplierStatusCommand(
    Guid SupplierId,
    SupplierStatus Status);
