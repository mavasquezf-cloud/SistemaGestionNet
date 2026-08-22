using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Application.Common.Time;
using SistemaGestion.Application.Customers.ChangeCustomerStatus;
using SistemaGestion.Application.Customers.CreateCustomer;
using SistemaGestion.Application.Customers.GetCustomerById;
using SistemaGestion.Application.Customers.GetCustomers;
using SistemaGestion.Application.Customers.Persistence;
using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Application.UnitTests.Customers;

public sealed class CustomerApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_normalizes_checks_uniqueness_uses_clock_and_saves_once()
    {
        var f = new Fixture();
        var result = await f.CreateUseCase().ExecuteAsync(new(" cus-001 ", " Customer ", " TAX ", " c@example.com ", " 555 ", " Quito "));
        Assert.Equal(CreateCustomerOutcome.Success, result.Outcome); Assert.Equal("CUS-001", result.Customer!.CustomerNumber);
        Assert.Equal("CUS-001", f.Repository.CheckedNumber!.Value); Assert.Equal(Now, result.Customer.CreatedAt);
        Assert.Equal("Customer", result.Customer.Name); Assert.Equal(1, f.Repository.AddCalls); Assert.Equal(1, f.Uow.Calls);
    }

    [Fact]
    public async Task Create_duplicate_normalized_number_rejects_without_save()
    {
        var f = new Fixture(); f.Repository.Items.Add(Customer("CUS-001"));
        var result = await f.CreateUseCase().ExecuteAsync(new(" cus-001 ", "Duplicate"));
        Assert.Equal(CreateCustomerOutcome.DuplicateCustomerNumber, result.Outcome); Assert.Null(result.Customer);
        Assert.Equal("CUS-001", f.Repository.CheckedNumber!.Value); Assert.Equal(0, f.Repository.AddCalls); Assert.Equal(0, f.Uow.Calls);
    }

    [Fact]
    public async Task Create_maps_authoritative_duplicate_race()
    {
        var f = new Fixture(); f.Uow.Exception = new CustomerDuplicateNumberException("duplicate");
        var result = await f.CreateUseCase().ExecuteAsync(new("CUS-1", "Customer"));
        Assert.Equal(CreateCustomerOutcome.DuplicateCustomerNumber, result.Outcome); Assert.Null(result.Customer); Assert.Equal(1, f.Uow.Calls);
    }

    [Fact]
    public async Task GetById_returns_active_and_inactive_and_not_found()
    {
        var f = new Fixture(); var active = Customer("CUS-1"); var inactive = Customer("CUS-2"); inactive.Deactivate(Now.AddHours(1));
        f.Repository.Items.AddRange([active, inactive]); var useCase = new GetCustomerByIdUseCase(f.Repository);
        Assert.Equal(CustomerStatus.Active, (await useCase.ExecuteAsync(new(active.Id))).Customer!.Status);
        Assert.Equal(CustomerStatus.Inactive, (await useCase.ExecuteAsync(new(inactive.Id))).Customer!.Status);
        var missing = await useCase.ExecuteAsync(new(Guid.NewGuid())); Assert.Equal(GetCustomerByIdOutcome.NotFound, missing.Outcome); Assert.Null(missing.Customer);
    }

    [Theory]
    [InlineData(2, 10, 2, 10)]
    [InlineData(0, 10, 1, 10)]
    [InlineData(1, 0, 1, 20)]
    [InlineData(1, 500, 1, 100)]
    public async Task GetCustomers_normalizes_pagination(int page, int size, int expectedPage, int expectedSize)
    {
        var f = new Fixture(); f.Repository.Items.AddRange([Customer("CUS-1"), Customer("CUS-2")]);
        var result = await new GetCustomersUseCase(f.Repository).ExecuteAsync(new(page, size));
        Assert.Equal(expectedPage, result.Page); Assert.Equal(expectedSize, result.PageSize);
        Assert.Equal(expectedPage, f.Repository.RequestedPage); Assert.Equal(expectedSize, f.Repository.RequestedSize);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task ChangeStatus_transitions_with_clock_and_saves()
    {
        var f = new Fixture(); var customer = Customer("CUS-1"); f.Repository.Items.Add(customer);
        var useCase = f.StatusUseCase(); var inactive = await useCase.ExecuteAsync(new(customer.Id, CustomerStatus.Inactive));
        Assert.Equal(CustomerStatus.Inactive, inactive.Customer!.Status); Assert.Equal(Now, inactive.Customer.UpdatedAt);
        var active = await useCase.ExecuteAsync(new(customer.Id, CustomerStatus.Active)); Assert.Equal(CustomerStatus.Active, active.Customer!.Status);
        Assert.Equal(2, f.Uow.Calls);
    }

    [Theory]
    [InlineData(CustomerStatus.Active)]
    [InlineData(CustomerStatus.Inactive)]
    public async Task ChangeStatus_idempotent_request_preserves_timestamp_and_still_saves(CustomerStatus status)
    {
        var f = new Fixture(); var customer = Customer("CUS-1");
        if (status == CustomerStatus.Inactive) customer.Deactivate(Now.AddHours(-1));
        var expected = customer.UpdatedAt; f.Repository.Items.Add(customer);
        var result = await f.StatusUseCase().ExecuteAsync(new(customer.Id, status));
        Assert.Equal(ChangeCustomerStatusOutcome.Success, result.Outcome); Assert.Equal(expected, result.Customer!.UpdatedAt); Assert.Equal(1, f.Uow.Calls);
    }

    [Fact]
    public async Task ChangeStatus_missing_does_not_save_and_concurrency_maps()
    {
        var missing = new Fixture(); var missingResult = await missing.StatusUseCase().ExecuteAsync(new(Guid.NewGuid(), CustomerStatus.Inactive));
        Assert.Equal(ChangeCustomerStatusOutcome.CustomerNotFound, missingResult.Outcome); Assert.Equal(0, missing.Uow.Calls);
        var concurrent = new Fixture(); var customer = Customer("CUS-1"); concurrent.Repository.Items.Add(customer);
        concurrent.Uow.Exception = new CustomerConcurrencyException("race");
        var result = await concurrent.StatusUseCase().ExecuteAsync(new(customer.Id, CustomerStatus.Inactive));
        Assert.Equal(ChangeCustomerStatusOutcome.ConcurrencyConflict, result.Outcome); Assert.Null(result.Customer); Assert.Equal(1, concurrent.Uow.Calls);
    }

    private static Customer Customer(string number) => new(Guid.NewGuid(), new(number), "Customer", Now);

    private sealed class Fixture
    {
        public FakeRepository Repository { get; } = new(); public FakeUow Uow { get; } = new();
        public CreateCustomerUseCase CreateUseCase() => new(Repository, Uow, new FakeClock(Now));
        public ChangeCustomerStatusUseCase StatusUseCase() => new(Repository, Uow, new FakeClock(Now));
    }
    private sealed class FakeRepository : ICustomerRepository
    {
        public List<Customer> Items { get; } = []; public int AddCalls { get; private set; }
        public CustomerNumber? CheckedNumber { get; private set; } public int? RequestedPage { get; private set; } public int? RequestedSize { get; private set; }
        public Task AddAsync(Customer customer, CancellationToken ct = default) { AddCalls++; Items.Add(customer); return Task.CompletedTask; }
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id));
        public Task<CustomerPage> GetPageAsync(int page, int size, CancellationToken ct = default) { RequestedPage = page; RequestedSize = size; return Task.FromResult(new CustomerPage(Items.Skip((page - 1) * size).Take(size).ToArray(), Items.Count)); }
        public Task<bool> ExistsByCustomerNumberAsync(CustomerNumber number, CancellationToken ct = default) { CheckedNumber = number; return Task.FromResult(Items.Any(x => x.CustomerNumber == number)); }
    }
    private sealed class FakeUow : IUnitOfWork
    {
        public int Calls { get; private set; } public Exception? Exception { get; set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) { Calls++; if (Exception is not null) throw Exception; return Task.FromResult(1); }
    }
    private sealed class FakeClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; } = now; }
}
