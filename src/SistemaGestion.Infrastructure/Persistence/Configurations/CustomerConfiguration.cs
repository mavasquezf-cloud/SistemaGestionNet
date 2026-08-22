using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SistemaGestion.Domain.Customers;

namespace SistemaGestion.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        var customerNumberConverter = new ValueConverter<CustomerNumber, string>(
            customerNumber => customerNumber.Value,
            value => new CustomerNumber(value));

        builder.ToTable("Customers");
        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id)
            .ValueGeneratedNever();

        builder.Property(customer => customer.CustomerNumber)
            .HasConversion(customerNumberConverter)
            .HasColumnName("CustomerNumber")
            .HasColumnType("varchar(50)")
            .HasMaxLength(CustomerNumber.MaximumLength)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(customer => customer.CustomerNumber)
            .IsUnique()
            .HasDatabaseName("UX_Customers_CustomerNumber");

        builder.Property(customer => customer.Name)
            .HasMaxLength(Customer.MaximumNameLength)
            .IsRequired();

        builder.Property(customer => customer.TaxIdentificationNumber)
            .HasMaxLength(Customer.MaximumTaxIdentificationNumberLength);

        builder.Property(customer => customer.Email)
            .HasMaxLength(Customer.MaximumEmailLength);

        builder.Property(customer => customer.Phone)
            .HasMaxLength(Customer.MaximumPhoneLength);

        builder.Property(customer => customer.Address)
            .HasMaxLength(Customer.MaximumAddressLength);

        builder.Property(customer => customer.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(customer => customer.CreatedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(customer => customer.UpdatedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();
    }
}
