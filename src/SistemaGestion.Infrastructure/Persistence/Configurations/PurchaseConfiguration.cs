using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaGestion.Domain.Purchasing;
using SistemaGestion.Domain.Suppliers;

namespace SistemaGestion.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");
        builder.HasKey(purchase => purchase.Id);
        builder.Property(purchase => purchase.PurchaseNumber)
            .HasConversion(number => number.Value, value => new PurchaseNumber(value))
            .HasColumnType("varchar(50)").IsRequired();
        builder.HasIndex(purchase => purchase.PurchaseNumber).IsUnique()
            .HasDatabaseName("UX_Purchases_PurchaseNumber");
        builder.Property(purchase => purchase.SupplierId).IsRequired();
        builder.Property(purchase => purchase.SupplierName).HasMaxLength(200).IsRequired();
        builder.Property(purchase => purchase.SupplierDocumentReference).HasMaxLength(100);
        builder.Property(purchase => purchase.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(purchase => purchase.Total).HasPrecision(18, 4).IsRequired();
        builder.Property(purchase => purchase.Total).HasField("_total");
        builder.Property(purchase => purchase.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(purchase => purchase.UpdatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(purchase => purchase.ReceivedAt).HasColumnType("datetimeoffset");
        builder.Property<byte[]>("RowVersion").IsRowVersion().IsConcurrencyToken().IsRequired();
        builder.HasOne<Supplier>().WithMany().HasForeignKey(purchase => purchase.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(purchase => purchase.Lines).WithOne().HasForeignKey(line => line.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(purchase => purchase.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
