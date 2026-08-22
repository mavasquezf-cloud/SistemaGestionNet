namespace SistemaGestion.Application.Customers.Persistence;

public sealed class CustomerConcurrencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class CustomerDuplicateNumberException(string message, Exception? innerException = null)
    : Exception(message, innerException);
