using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Application.Customers.Persistence;

public sealed record CustomerPage(IReadOnlyList<Customer> Items, int TotalCount);
