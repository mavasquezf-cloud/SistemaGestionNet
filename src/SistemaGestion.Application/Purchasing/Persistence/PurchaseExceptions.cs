namespace SistemaGestion.Application.Purchasing.Persistence;

public sealed class PurchaseConcurrencyException(string message, Exception? innerException = null) : Exception(message, innerException);
public sealed class PurchaseDuplicateNumberException(string message, Exception? innerException = null) : Exception(message, innerException);
public sealed class PurchaseReceiptConflictException(string message, Exception? innerException = null) : Exception(message, innerException);
