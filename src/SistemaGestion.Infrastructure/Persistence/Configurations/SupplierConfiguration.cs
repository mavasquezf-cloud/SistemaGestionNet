using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        var supplierNumberConverter = new ValueConverter<SupplierNumber, string>(
            supplierNumber => supplierNumber.Value,
            value => new SupplierNumber(value));

        builder.ToTable("Suppliers");
        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.SupplierNumber)
            .HasConversion(supplierNumberConverter)
            .HasColumnName("SupplierNumber")
            .HasColumnType("varchar(50)")
            .HasMaxLength(SupplierNumber.MaximumLength)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(supplier => supplier.SupplierNumber)
            .IsUnique()
            .HasDatabaseName("UX_Suppliers_SupplierNumber");

        builder.Property(supplier => supplier.Name)
            .HasMaxLength(Supplier.MaximumNameLength)
            .IsRequired();

        builder.Property(supplier => supplier.TaxIdentificationNumber)
            .HasMaxLength(Supplier.MaximumTaxIdentificationNumberLength);

        builder.Property(supplier => supplier.Email)
            .HasMaxLength(Supplier.MaximumEmailLength);

        builder.Property(supplier => supplier.Phone)
            .HasMaxLength(Supplier.MaximumPhoneLength);

        builder.Property(supplier => supplier.Address)
            .HasMaxLength(Supplier.MaximumAddressLength);

        builder.Property(supplier => supplier.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(supplier => supplier.CreatedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(supplier => supplier.UpdatedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();
    }
}
