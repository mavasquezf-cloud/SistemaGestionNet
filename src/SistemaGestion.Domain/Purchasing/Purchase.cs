namespace SistemaGestion.Domain.Purchasing;

public sealed class Purchase
{
    public const int MaximumSupplierNameLength = 200;
    public const int MaximumSupplierDocumentReferenceLength = 100;

    private readonly List<PurchaseLine> _lines = [];
    private decimal _total;

    public Purchase(
        Guid id,
        PurchaseNumber purchaseNumber,
        Guid supplierId,
        string supplierName,
        DateTimeOffset createdAt,
        string? supplierDocumentReference = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Purchase ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(purchaseNumber);

        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("Supplier ID cannot be empty.", nameof(supplierId));
        }

        Id = id;
        PurchaseNumber = purchaseNumber;
        SupplierId = supplierId;
        SupplierName = NormalizeRequiredSupplierName(supplierName);
        SupplierDocumentReference = NormalizeReference(supplierDocumentReference);
        Status = PurchaseStatus.Draft;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }
    public PurchaseNumber PurchaseNumber { get; }
    public Guid SupplierId { get; }
    public string SupplierName { get; }
    public string? SupplierDocumentReference { get; }
    public PurchaseStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public IReadOnlyCollection<PurchaseLine> Lines => _lines.AsReadOnly();
    public decimal Total => _total;

    public PurchaseLine AddLine(
        Guid lineId,
        Guid productId,
        string productName,
        string unitOfMeasure,
        decimal quantity,
        decimal unitCost,
        DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseStatus.Draft, "Lines can only be added to a draft purchase.");

        if (_lines.Any(line => line.ProductId == productId))
        {
            throw new InvalidOperationException("A product can appear only once in a purchase.");
        }

        var line = new PurchaseLine(
            lineId, Id, productId, productName, unitOfMeasure, quantity, unitCost);

        _lines.Add(line);
        _total += line.LineTotal;
        UpdatedAt = occurredAt;
        return line;
    }

    public void Confirm(DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseStatus.Draft, "Only a draft purchase can be confirmed.");
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("A purchase must contain at least one line before confirmation.");
        }

        Status = PurchaseStatus.Confirmed;
        UpdatedAt = occurredAt;
    }

    public void Receive(DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseStatus.Confirmed, "Only a confirmed purchase can be received.");
        Status = PurchaseStatus.Received;
        ReceivedAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    public void Cancel(DateTimeOffset occurredAt)
    {
        if (Status is not PurchaseStatus.Draft and not PurchaseStatus.Confirmed)
        {
            throw new InvalidOperationException("Only a draft or confirmed purchase can be cancelled.");
        }

        Status = PurchaseStatus.Cancelled;
        UpdatedAt = occurredAt;
    }

    private void EnsureStatus(PurchaseStatus requiredStatus, string message)
    {
        if (Status != requiredStatus)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string NormalizeRequiredSupplierName(string supplierName)
    {
        if (string.IsNullOrWhiteSpace(supplierName))
        {
            throw new ArgumentException("Supplier name is required.", nameof(supplierName));
        }

        var normalized = supplierName.Trim();
        if (normalized.Length > MaximumSupplierNameLength)
        {
            throw new ArgumentException(
                $"Supplier name cannot exceed {MaximumSupplierNameLength} characters.", nameof(supplierName));
        }

        return normalized;
    }

    private static string? NormalizeReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var normalized = reference.Trim();
        if (normalized.Length > MaximumSupplierDocumentReferenceLength)
        {
            throw new ArgumentException(
                $"Supplier document reference cannot exceed {MaximumSupplierDocumentReferenceLength} characters.",
                nameof(reference));
        }

        return normalized;
    }
}
