using System.Reflection;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Domain.UnitTests.Inventory;

public sealed class InventoryItemTests
{
    [Fact]
    public void Constructor_WithValidIds_StartsAtZero()
    {
        var item = CreateItem();

        Assert.Equal(0m, item.QuantityOnHand);
        Assert.Empty(item.RowVersion);
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new InventoryItem(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_WithEmptyProductId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new InventoryItem(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void ApplyManualAdjustment_WithPositiveDelta_IncreasesBalanceAndCreatesMovement()
    {
        var item = CreateItem();
        var movementId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 8, 16, 10, 30, 0, TimeSpan.Zero);

        var movement = item.ApplyManualAdjustment(
            movementId, 12.5m, " Initial count ", " COUNT-001 ", occurredAt);

        Assert.Equal(12.5m, item.QuantityOnHand);
        Assert.Equal(movementId, movement.Id);
        Assert.Equal(item.Id, movement.InventoryItemId);
        Assert.Equal(item.ProductId, movement.ProductId);
        Assert.Equal(12.5m, movement.QuantityDelta);
        Assert.Equal(12.5m, movement.ResultingBalance);
        Assert.Equal(InventoryMovementType.Increase, movement.Type);
        Assert.Equal(MovementSource.ManualAdjustment, movement.Source);
        Assert.Equal("Initial count", movement.Reason);
        Assert.Equal("COUNT-001", movement.Reference);
        Assert.Equal(occurredAt, movement.OccurredAt);
    }

    [Fact]
    public void ApplyManualAdjustment_WithNegativeDelta_DecreasesBalanceAndCreatesMovement()
    {
        var item = CreateItem();
        item.ApplyManualAdjustment(Guid.NewGuid(), 10m, "Initial count", null, DateTimeOffset.UtcNow);

        var movement = item.ApplyManualAdjustment(
            Guid.NewGuid(), -3.25m, "Damaged units", null, DateTimeOffset.UtcNow);

        Assert.Equal(6.75m, item.QuantityOnHand);
        Assert.Equal(-3.25m, movement.QuantityDelta);
        Assert.Equal(6.75m, movement.ResultingBalance);
        Assert.Equal(InventoryMovementType.Decrease, movement.Type);
        Assert.Equal(MovementSource.ManualAdjustment, movement.Source);
    }

    [Fact]
    public void ApplyManualAdjustment_WithSequentialMovements_CalculatesEachResultingBalance()
    {
        var item = CreateItem();

        var first = Adjust(item, 10m);
        var second = Adjust(item, -2.5m);
        var third = Adjust(item, 4m);

        Assert.Equal(10m, first.ResultingBalance);
        Assert.Equal(7.5m, second.ResultingBalance);
        Assert.Equal(11.5m, third.ResultingBalance);
        Assert.Equal(11.5m, item.QuantityOnHand);
    }

    [Fact]
    public void ApplyManualAdjustment_WithEmptyMovementId_ThrowsArgumentException()
    {
        var item = CreateItem();

        Assert.Throws<ArgumentException>(() => item.ApplyManualAdjustment(
            Guid.Empty, 1m, "Count", null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApplyManualAdjustment_WithZeroDelta_ThrowsArgumentOutOfRangeException()
    {
        var item = CreateItem();

        Assert.Throws<ArgumentOutOfRangeException>(() => Adjust(item, 0m));
    }

    [Fact]
    public void ApplyManualAdjustment_WhenResultWouldBeNegative_ThrowsAndPreservesBalance()
    {
        var item = CreateItem();
        Adjust(item, 2m);

        var exception = Assert.Throws<InsufficientStockException>(() => Adjust(item, -2.01m));

        Assert.Equal(2m, exception.QuantityOnHand);
        Assert.Equal(-2.01m, exception.QuantityDelta);
        Assert.Equal(2m, item.QuantityOnHand);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyManualAdjustment_WithMissingReason_ThrowsArgumentException(string? reason)
    {
        var item = CreateItem();

        Assert.Throws<ArgumentException>(() => item.ApplyManualAdjustment(
            Guid.NewGuid(), 1m, reason!, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApplyManualAdjustment_WithReasonOverMaximumLength_ThrowsArgumentException()
    {
        var item = CreateItem();

        Assert.Throws<ArgumentException>(() => item.ApplyManualAdjustment(
            Guid.NewGuid(), 1m, new string('r', InventoryItem.MaximumReasonLength + 1),
            null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApplyManualAdjustment_WithReferenceOverMaximumLength_ThrowsArgumentException()
    {
        var item = CreateItem();

        Assert.Throws<ArgumentException>(() => item.ApplyManualAdjustment(
            Guid.NewGuid(), 1m, "Count",
            new string('r', InventoryItem.MaximumReferenceLength + 1), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApplyManualAdjustment_WithWhitespaceReference_NormalizesItToNull()
    {
        var movement = CreateItem().ApplyManualAdjustment(
            Guid.NewGuid(), 1m, "Count", "   ", DateTimeOffset.UtcNow);

        Assert.Null(movement.Reference);
    }

    [Fact]
    public void ApplyPurchaseReceipt_WithPositiveQuantity_IncreasesStockAndCreatesPurchaseMovement()
    {
        var item = CreateItem();
        item.ApplyManualAdjustment(Guid.NewGuid(), 2m, "Initial count", null, DateTimeOffset.UtcNow);
        var occurredAt = new DateTimeOffset(2026, 8, 18, 14, 0, 0, TimeSpan.Zero);

        var movement = item.ApplyPurchaseReceipt(
            Guid.NewGuid(), 5.5m, "  PUR-001  ", "  Goods received  ", occurredAt);

        Assert.Equal(7.5m, item.QuantityOnHand);
        Assert.Equal(5.5m, movement.QuantityDelta);
        Assert.Equal(7.5m, movement.ResultingBalance);
        Assert.Equal(InventoryMovementType.Increase, movement.Type);
        Assert.Equal(MovementSource.PurchaseReceipt, movement.Source);
        Assert.Equal("PUR-001", movement.Reference);
        Assert.Equal("Goods received", movement.Reason);
        Assert.Equal(occurredAt, movement.OccurredAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyPurchaseReceipt_WithNonPositiveQuantity_ThrowsArgumentOutOfRangeException(decimal quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateItem().ApplyPurchaseReceipt(
            Guid.NewGuid(), quantity, "PUR-001", "Receipt", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApplyPurchaseReceipt_WithEmptyMovementId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CreateItem().ApplyPurchaseReceipt(
            Guid.Empty, 1m, "PUR-001", "Receipt", DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyPurchaseReceipt_WithMissingReference_ThrowsArgumentException(string? reference)
    {
        Assert.Throws<ArgumentException>(() => CreateItem().ApplyPurchaseReceipt(
            Guid.NewGuid(), 1m, reference!, "Receipt", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApplyPurchaseReceipt_WithReferenceOverMaximum_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CreateItem().ApplyPurchaseReceipt(
            Guid.NewGuid(), 1m, new string('R', InventoryItem.MaximumReferenceLength + 1),
            "Receipt", DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyPurchaseReceipt_WithMissingReason_ThrowsArgumentException(string? reason)
    {
        Assert.Throws<ArgumentException>(() => CreateItem().ApplyPurchaseReceipt(
            Guid.NewGuid(), 1m, "PUR-001", reason!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApplyPurchaseReceipt_WithReasonOverMaximum_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CreateItem().ApplyPurchaseReceipt(
            Guid.NewGuid(), 1m, "PUR-001", new string('R', InventoryItem.MaximumReasonLength + 1),
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApplyManualAdjustment_AfterExtension_RemainsManualAdjustment()
    {
        var movement = Adjust(CreateItem(), 1m);

        Assert.Equal(MovementSource.ManualAdjustment, movement.Source);
    }

    [Fact]
    public void QuantityOnHand_HasNoPublicSetter()
    {
        var property = typeof(InventoryItem).GetProperty(nameof(InventoryItem.QuantityOnHand));

        Assert.NotNull(property);
        Assert.False(property.SetMethod?.IsPublic ?? false);
    }

    [Fact]
    public void InventoryMovement_PropertiesHaveNoPublicSetters()
    {
        var publicProperties = typeof(InventoryMovement).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(publicProperties);
        Assert.All(publicProperties, property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    [Fact]
    public void InventoryMovement_HasNoPublicConstructors()
    {
        Assert.Empty(typeof(InventoryMovement).GetConstructors());
    }

    [Fact]
    public void InventoryItem_HasNoPublicSetStockMethod()
    {
        var method = typeof(InventoryItem).GetMethod(
            "SetStock", BindingFlags.Public | BindingFlags.Instance);

        Assert.Null(method);
    }

    private static InventoryItem CreateItem() => new(Guid.NewGuid(), Guid.NewGuid());

    private static InventoryMovement Adjust(InventoryItem item, decimal quantityDelta) =>
        item.ApplyManualAdjustment(
            Guid.NewGuid(), quantityDelta, "Inventory count", null, DateTimeOffset.UtcNow);
}
