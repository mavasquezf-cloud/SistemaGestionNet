using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Infrastructure.Persistence.Configurations;

internal sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_InventoryItems_QuantityOnHand_NonNegative",
                "[QuantityOnHand] >= 0"));

        builder.HasKey(item => item.Id);

        builder.Property(item => item.ProductId)
            .IsRequired();

        builder.HasIndex(item => item.ProductId)
            .IsUnique()
            .HasDatabaseName("IX_InventoryItems_ProductId");

        builder.Property(item => item.QuantityOnHand)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(item => item.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
