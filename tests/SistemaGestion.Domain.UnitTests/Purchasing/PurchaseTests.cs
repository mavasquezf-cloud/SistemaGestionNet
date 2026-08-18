using System.Collections;
using System.Reflection;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Domain.UnitTests.Purchasing;

public sealed class PurchaseTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidValues_CreatesNormalizedDraft()
    {
        var purchase = CreatePurchase(
            supplierName: "  Example Supplier  ", reference: "  INV-100  ");

        Assert.Equal("Example Supplier", purchase.SupplierName);
        Assert.Equal("INV-100", purchase.SupplierDocumentReference);
        Assert.Equal(PurchaseStatus.Draft, purchase.Status);
        Assert.Empty(purchase.Lines);
        Assert.Equal(0m, purchase.Total);
        Assert.Equal(CreatedAt, purchase.CreatedAt);
        Assert.Equal(CreatedAt, purchase.UpdatedAt);
        Assert.Null(purchase.ReceivedAt);
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => CreatePurchase(id: Guid.Empty));

    [Fact]
    public void Constructor_WithNullPurchaseNumber_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new Purchase(
            Guid.NewGuid(), null!, Guid.NewGuid(), "Supplier", CreatedAt));

    [Fact]
    public void Constructor_WithEmptySupplierId_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => CreatePurchase(supplierId: Guid.Empty));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingSupplierName_ThrowsArgumentException(string? name) =>
        Assert.Throws<ArgumentException>(() => CreatePurchase(supplierName: name!));

    [Fact]
    public void Constructor_WithSupplierNameOverMaximum_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => CreatePurchase(
            supplierName: new string('S', Purchase.MaximumSupplierNameLength + 1)));

    [Fact]
    public void Constructor_WithWhitespaceReference_NormalizesToNull() =>
        Assert.Null(CreatePurchase(reference: "   ").SupplierDocumentReference);

    [Fact]
    public void Constructor_WithReferenceOverMaximum_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => CreatePurchase(
            reference: new string('R', Purchase.MaximumSupplierDocumentReferenceLength + 1)));

    [Fact]
    public void AddLine_WithValidValues_AddsImmutableSnapshotAndUpdatesTimestamp()
    {
        var purchase = CreatePurchase();
        var lineId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var occurredAt = CreatedAt.AddMinutes(1);

        var line = purchase.AddLine(
            lineId, productId, "  Widget  ", "  EA  ", 2m, 3.5m, occurredAt);

        Assert.Same(line, Assert.Single(purchase.Lines));
        Assert.Equal(lineId, line.Id);
        Assert.Equal(purchase.Id, line.PurchaseId);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal("Widget", line.ProductName);
        Assert.Equal("EA", line.UnitOfMeasure);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(3.5m, line.UnitCost);
        Assert.Equal(7m, line.LineTotal);
        Assert.Equal(occurredAt, purchase.UpdatedAt);
    }

    [Fact]
    public void AddLine_WithEmptyLineId_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => AddLine(CreatePurchase(), lineId: Guid.Empty));

    [Fact]
    public void AddLine_WithEmptyProductId_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => AddLine(CreatePurchase(), productId: Guid.Empty));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLine_WithMissingProductName_ThrowsArgumentException(string? name) =>
        Assert.Throws<ArgumentException>(() => AddLine(CreatePurchase(), productName: name!));

    [Fact]
    public void AddLine_WithProductNameOverMaximum_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => AddLine(
            CreatePurchase(), productName: new string('P', PurchaseLine.MaximumProductNameLength + 1)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLine_WithMissingUnitOfMeasure_ThrowsArgumentException(string? unit) =>
        Assert.Throws<ArgumentException>(() => AddLine(CreatePurchase(), unitOfMeasure: unit!));

    [Fact]
    public void AddLine_WithUnitOfMeasureOverMaximum_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => AddLine(
            CreatePurchase(), unitOfMeasure: new string('U', PurchaseLine.MaximumUnitOfMeasureLength + 1)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddLine_WithNonPositiveQuantity_ThrowsArgumentOutOfRangeException(decimal quantity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AddLine(CreatePurchase(), quantity: quantity));

    [Fact]
    public void AddLine_WithNegativeUnitCost_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AddLine(CreatePurchase(), unitCost: -0.01m));

    [Fact]
    public void AddLine_WithZeroUnitCost_IsAllowed()
    {
        var line = AddLine(CreatePurchase(), unitCost: 0m);
        Assert.Equal(0m, line.LineTotal);
    }

    [Fact]
    public void AddLine_WithDuplicateProduct_ThrowsInvalidOperationException()
    {
        var purchase = CreatePurchase();
        var productId = Guid.NewGuid();
        AddLine(purchase, productId: productId);

        Assert.Throws<InvalidOperationException>(() => AddLine(purchase, productId: productId));
    }

    [Fact]
    public void AddLine_RoundsLineTotalToFourDecimalsAwayFromZero()
    {
        var line = AddLine(CreatePurchase(), quantity: 1m, unitCost: 1.23455m);

        Assert.Equal(1.2346m, line.LineTotal);
    }

    [Fact]
    public void Total_SumsAlreadyRoundedLineTotals()
    {
        var purchase = CreatePurchase();
        AddLine(purchase, productId: Guid.NewGuid(), quantity: 1m, unitCost: 1.23455m);
        AddLine(purchase, productId: Guid.NewGuid(), quantity: 1m, unitCost: 2.34565m);

        Assert.Equal(3.5803m, purchase.Total);
    }

    [Fact]
    public void Confirm_EmptyDraft_ThrowsAndPreservesState()
    {
        var purchase = CreatePurchase();
        Assert.Throws<InvalidOperationException>(() => purchase.Confirm(CreatedAt.AddHours(1)));
        Assert.Equal(PurchaseStatus.Draft, purchase.Status);
    }

    [Fact]
    public void Confirm_DraftWithLine_ChangesStatusAndUpdatedAt()
    {
        var purchase = DraftWithLine();
        var occurredAt = CreatedAt.AddHours(1);
        purchase.Confirm(occurredAt);
        Assert.Equal(PurchaseStatus.Confirmed, purchase.Status);
        Assert.Equal(occurredAt, purchase.UpdatedAt);
    }

    [Fact]
    public void Receive_ConfirmedPurchase_SetsTerminalStateAndTimestamps()
    {
        var purchase = ConfirmedPurchase();
        var occurredAt = CreatedAt.AddHours(2);
        purchase.Receive(occurredAt);
        Assert.Equal(PurchaseStatus.Received, purchase.Status);
        Assert.Equal(occurredAt, purchase.ReceivedAt);
        Assert.Equal(occurredAt, purchase.UpdatedAt);
    }

    [Fact]
    public void Receive_Draft_ThrowsInvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => CreatePurchase().Receive(CreatedAt));

    [Fact]
    public void Receive_ReceivedPurchaseAgain_ThrowsInvalidOperationException()
    {
        var purchase = ConfirmedPurchase();
        purchase.Receive(CreatedAt.AddHours(2));
        Assert.Throws<InvalidOperationException>(() => purchase.Receive(CreatedAt.AddHours(3)));
    }

    [Fact]
    public void Cancel_Draft_ChangesStatusAndUpdatedAt()
    {
        var purchase = CreatePurchase();
        var occurredAt = CreatedAt.AddHours(1);
        purchase.Cancel(occurredAt);
        Assert.Equal(PurchaseStatus.Cancelled, purchase.Status);
        Assert.Equal(occurredAt, purchase.UpdatedAt);
    }

    [Fact]
    public void Cancel_Confirmed_ChangesStatus()
    {
        var purchase = ConfirmedPurchase();
        purchase.Cancel(CreatedAt.AddHours(2));
        Assert.Equal(PurchaseStatus.Cancelled, purchase.Status);
    }

    [Fact]
    public void Cancel_Received_ThrowsInvalidOperationException()
    {
        var purchase = ConfirmedPurchase();
        purchase.Receive(CreatedAt.AddHours(2));
        Assert.Throws<InvalidOperationException>(() => purchase.Cancel(CreatedAt.AddHours(3)));
    }

    [Fact]
    public void CancelledPurchase_CannotConfirmReceiveOrAddLines()
    {
        var purchase = DraftWithLine();
        purchase.Cancel(CreatedAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => purchase.Confirm(CreatedAt.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => purchase.Receive(CreatedAt.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => AddLine(purchase));
    }

    [Fact]
    public void ReceivedPurchase_CannotAcceptLines()
    {
        var purchase = ConfirmedPurchase();
        purchase.Receive(CreatedAt.AddHours(2));
        Assert.Throws<InvalidOperationException>(() => AddLine(purchase));
    }

    [Fact]
    public void ConfirmedPurchase_CannotAcceptLines()
    {
        Assert.Throws<InvalidOperationException>(() => AddLine(ConfirmedPurchase()));
    }

    [Fact]
    public void PublicProperties_HaveNoPublicSetters()
    {
        Assert.All(typeof(Purchase).GetProperties(), property =>
            Assert.False(property.SetMethod?.IsPublic ?? false));
        Assert.All(typeof(PurchaseLine).GetProperties(), property =>
            Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    [Fact]
    public void Purchase_HasNoPublicStatusOrTotalSetterMethods()
    {
        Assert.Null(typeof(Purchase).GetMethod("SetStatus", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(Purchase).GetMethod("SetTotal", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void PurchaseLine_HasNoPublicConstructorsAndLinesCollectionCannotBeMutated()
    {
        var purchase = DraftWithLine();

        Assert.Empty(typeof(PurchaseLine).GetConstructors());
        Assert.False(purchase.Lines is IList mutable && !mutable.IsReadOnly);
    }

    private static Purchase CreatePurchase(
        Guid? id = null,
        Guid? supplierId = null,
        string supplierName = "Example Supplier",
        string? reference = null) =>
        new(id ?? Guid.NewGuid(), new PurchaseNumber("PUR-001"),
            supplierId ?? Guid.NewGuid(), supplierName, CreatedAt, reference);

    private static PurchaseLine AddLine(
        Purchase purchase,
        Guid? lineId = null,
        Guid? productId = null,
        string productName = "Widget",
        string unitOfMeasure = "EA",
        decimal quantity = 2m,
        decimal unitCost = 5m) =>
        purchase.AddLine(lineId ?? Guid.NewGuid(), productId ?? Guid.NewGuid(), productName,
            unitOfMeasure, quantity, unitCost, CreatedAt.AddMinutes(1));

    private static Purchase DraftWithLine()
    {
        var purchase = CreatePurchase();
        AddLine(purchase);
        return purchase;
    }

    private static Purchase ConfirmedPurchase()
    {
        var purchase = DraftWithLine();
        purchase.Confirm(CreatedAt.AddHours(1));
        return purchase;
    }
}
