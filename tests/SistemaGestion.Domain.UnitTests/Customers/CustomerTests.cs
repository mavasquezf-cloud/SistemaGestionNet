using System.Reflection;
using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Domain.UnitTests.Customers;

public sealed class CustomerTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidValues_CreatesActiveNormalizedCustomer()
    {
        var id = Guid.NewGuid(); var number = new CustomerNumber("CUS-001");
        var customer = new Customer(id, number, " Customer ", CreatedAt,
            " TAX-1 ", " customer@example.com ", " 555-0100 ", " Quito ");
        Assert.Equal(id, customer.Id); Assert.Same(number, customer.CustomerNumber);
        Assert.Equal("Customer", customer.Name); Assert.Equal("TAX-1", customer.TaxIdentificationNumber);
        Assert.Equal("customer@example.com", customer.Email); Assert.Equal("555-0100", customer.Phone);
        Assert.Equal("Quito", customer.Address); Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.Equal(CreatedAt, customer.CreatedAt); Assert.Equal(CreatedAt, customer.UpdatedAt);
    }

    [Fact] public void Constructor_WithEmptyId_Throws() => Assert.Throws<ArgumentException>(() => Create(id: Guid.Empty));
    [Fact] public void Constructor_WithNullNumber_Throws() => Assert.Throws<ArgumentNullException>(() => new Customer(Guid.NewGuid(), null!, "Customer", CreatedAt));
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string? name) => Assert.Throws<ArgumentException>(() => Create(name: name!));

    [Fact]
    public void Constructor_WithWhitespaceOptionalValues_UsesNull()
    {
        var customer = Create(tax: " ", email: " ", phone: " ", address: " ");
        Assert.Null(customer.TaxIdentificationNumber); Assert.Null(customer.Email);
        Assert.Null(customer.Phone); Assert.Null(customer.Address);
    }

    [Fact] public void Constructor_AcceptsValidEmail() => Assert.Equal("valid@example.com", Create(email: " valid@example.com ").Email);
    [Theory]
    [InlineData("not-an-email")] [InlineData("Name <valid@example.com>")] [InlineData("valid @example.com")]
    public void Constructor_WithInvalidEmail_Throws(string email) => Assert.Throws<ArgumentException>(() => Create(email: email));

    [Theory]
    [InlineData("name")] [InlineData("tax")] [InlineData("email")] [InlineData("phone")] [InlineData("address")]
    public void Constructor_OverMaximumLengths_Throws(string field)
    {
        Assert.Throws<ArgumentException>(() => field switch
        {
            "name" => Create(name: new string('N', Customer.MaximumNameLength + 1)),
            "tax" => Create(tax: new string('T', Customer.MaximumTaxIdentificationNumberLength + 1)),
            "email" => Create(email: new string('e', Customer.MaximumEmailLength + 1)),
            "phone" => Create(phone: new string('1', Customer.MaximumPhoneLength + 1)),
            _ => Create(address: new string('A', Customer.MaximumAddressLength + 1))
        });
    }

    [Fact]
    public void Deactivate_AndActivate_UpdateOnlyOnRealTransitions()
    {
        var customer = Create(); var inactiveAt = CreatedAt.AddHours(1); var activeAt = CreatedAt.AddHours(3);
        customer.Deactivate(inactiveAt); Assert.Equal(CustomerStatus.Inactive, customer.Status); Assert.Equal(inactiveAt, customer.UpdatedAt);
        customer.Deactivate(CreatedAt.AddHours(2)); Assert.Equal(inactiveAt, customer.UpdatedAt);
        customer.Activate(activeAt); Assert.Equal(CustomerStatus.Active, customer.Status); Assert.Equal(activeAt, customer.UpdatedAt);
        customer.Activate(CreatedAt.AddHours(4)); Assert.Equal(activeAt, customer.UpdatedAt);
    }

    [Fact]
    public void Activate_WhenInitiallyActive_IsIdempotent()
    {
        var customer = Create(); customer.Activate(CreatedAt.AddHours(1));
        Assert.Equal(CreatedAt, customer.UpdatedAt);
    }

    [Fact]
    public void PublicProperties_HaveNoPublicSetters()
    {
        var properties = typeof(Customer).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.NotEmpty(properties); Assert.All(properties, property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    [Fact]
    public void Customer_HasNoPublicSetStatusMethod() =>
        Assert.Null(typeof(Customer).GetMethod("SetStatus", BindingFlags.Public | BindingFlags.Instance));

    private static Customer Create(Guid? id = null, string name = "Customer", string? tax = null,
        string? email = null, string? phone = null, string? address = null) =>
        new(id ?? Guid.NewGuid(), new CustomerNumber("CUS-001"), name, CreatedAt, tax, email, phone, address);
}
