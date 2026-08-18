using System.Reflection;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Domain.UnitTests.Suppliers;

public sealed class SupplierTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidValues_CreatesActiveSupplierWithSuppliedTimestamp()
    {
        var id = Guid.NewGuid();
        var supplierNumber = new SupplierNumber("SUP-001");

        var supplier = new Supplier(
            id,
            supplierNumber,
            "  Example Supplier  ",
            CreatedAt,
            "  1234567890  ",
            "  sales@example.com  ",
            "  +593 2 555 0100  ",
            "  Quito, Ecuador  ");

        Assert.Equal(id, supplier.Id);
        Assert.Same(supplierNumber, supplier.SupplierNumber);
        Assert.Equal("Example Supplier", supplier.Name);
        Assert.Equal("1234567890", supplier.TaxIdentificationNumber);
        Assert.Equal("sales@example.com", supplier.Email);
        Assert.Equal("+593 2 555 0100", supplier.Phone);
        Assert.Equal("Quito, Ecuador", supplier.Address);
        Assert.Equal(SupplierStatus.Active, supplier.Status);
        Assert.Equal(CreatedAt, supplier.CreatedAt);
        Assert.Equal(CreatedAt, supplier.UpdatedAt);
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CreateSupplier(id: Guid.Empty));
    }

    [Fact]
    public void Constructor_WithNullSupplierNumber_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Supplier(
            Guid.NewGuid(), null!, "Supplier", CreatedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingName_ThrowsArgumentException(string? name)
    {
        Assert.Throws<ArgumentException>(() => CreateSupplier(name: name!));
    }

    [Fact]
    public void Constructor_WithNameOverMaximumLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateSupplier(name: new string('N', Supplier.MaximumNameLength + 1)));
    }

    [Fact]
    public void Constructor_WithWhitespaceOptionalFields_NormalizesThemToNull()
    {
        var supplier = CreateSupplier(
            taxIdentificationNumber: "   ",
            email: "   ",
            phone: "   ",
            address: "   ");

        Assert.Null(supplier.TaxIdentificationNumber);
        Assert.Null(supplier.Email);
        Assert.Null(supplier.Phone);
        Assert.Null(supplier.Address);
    }

    [Fact]
    public void Constructor_WithTaxIdentificationNumberOverMaximumLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CreateSupplier(
            taxIdentificationNumber: new string(
                'T', Supplier.MaximumTaxIdentificationNumberLength + 1)));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Display Name <sales@example.com>")]
    [InlineData("sales @example.com")]
    public void Constructor_WithInvalidEmail_ThrowsArgumentException(string email)
    {
        Assert.Throws<ArgumentException>(() => CreateSupplier(email: email));
    }

    [Fact]
    public void Constructor_WithEmailOverMaximumLength_ThrowsArgumentException()
    {
        var email = $"{new string('a', Supplier.MaximumEmailLength)}@example.com";

        Assert.Throws<ArgumentException>(() => CreateSupplier(email: email));
    }

    [Fact]
    public void Constructor_WithPhoneOverMaximumLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateSupplier(phone: new string('1', Supplier.MaximumPhoneLength + 1)));
    }

    [Fact]
    public void Constructor_WithAddressOverMaximumLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateSupplier(address: new string('A', Supplier.MaximumAddressLength + 1)));
    }

    [Fact]
    public void Deactivate_WhenActive_ChangesStatusAndUpdatedAt()
    {
        var supplier = CreateSupplier();
        var occurredAt = CreatedAt.AddHours(1);

        supplier.Deactivate(occurredAt);

        Assert.Equal(SupplierStatus.Inactive, supplier.Status);
        Assert.Equal(occurredAt, supplier.UpdatedAt);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_IsIdempotentAndPreservesUpdatedAt()
    {
        var supplier = CreateSupplier();
        var firstChange = CreatedAt.AddHours(1);
        supplier.Deactivate(firstChange);

        supplier.Deactivate(CreatedAt.AddHours(2));

        Assert.Equal(SupplierStatus.Inactive, supplier.Status);
        Assert.Equal(firstChange, supplier.UpdatedAt);
    }

    [Fact]
    public void Activate_WhenInactive_ChangesStatusAndUpdatedAt()
    {
        var supplier = CreateSupplier();
        supplier.Deactivate(CreatedAt.AddHours(1));
        var occurredAt = CreatedAt.AddHours(2);

        supplier.Activate(occurredAt);

        Assert.Equal(SupplierStatus.Active, supplier.Status);
        Assert.Equal(occurredAt, supplier.UpdatedAt);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsIdempotentAndPreservesUpdatedAt()
    {
        var supplier = CreateSupplier();

        supplier.Activate(CreatedAt.AddHours(1));

        Assert.Equal(SupplierStatus.Active, supplier.Status);
        Assert.Equal(CreatedAt, supplier.UpdatedAt);
    }

    [Fact]
    public void PublicProperties_HaveNoPublicSetters()
    {
        var properties = typeof(Supplier).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(properties);
        Assert.All(properties, property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    [Fact]
    public void Supplier_HasNoPublicSetStatusMethod()
    {
        var method = typeof(Supplier).GetMethod(
            "SetStatus", BindingFlags.Public | BindingFlags.Instance);

        Assert.Null(method);
    }

    private static Supplier CreateSupplier(
        Guid? id = null,
        string name = "Example Supplier",
        string? taxIdentificationNumber = null,
        string? email = null,
        string? phone = null,
        string? address = null)
    {
        return new Supplier(
            id ?? Guid.NewGuid(),
            new SupplierNumber("SUP-001"),
            name,
            CreatedAt,
            taxIdentificationNumber,
            email,
            phone,
            address);
    }
}
