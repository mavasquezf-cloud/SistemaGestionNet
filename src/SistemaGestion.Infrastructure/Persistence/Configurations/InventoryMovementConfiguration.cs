using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Inventory;

namespace SistemaGestion.Infrastructure.Persistence.Configurations;

internal sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_InventoryMovements_QuantityDelta_NonZero",
                "[QuantityDelta] <> 0");
            tableBuilder.HasCheckConstraint(
                "CK_InventoryMovements_ResultingBalance_NonNegative",
                "[ResultingBalance] >= 0");
        });

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.InventoryItemId)
            .IsRequired();

        builder.Property(movement => movement.ProductId)
            .IsRequired();

        builder.Property(movement => movement.QuantityDelta)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(movement => movement.ResultingBalance)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(movement => movement.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(movement => movement.Source)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(movement => movement.Reference)
            .HasMaxLength(InventoryItem.MaximumReferenceLength);

        builder.Property(movement => movement.Reason)
            .HasMaxLength(InventoryItem.MaximumReasonLength)
            .IsRequired();

        builder.Property(movement => movement.OccurredAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(movement => movement.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(movement => new { movement.ProductId, movement.OccurredAt })
            .HasDatabaseName("IX_InventoryMovements_ProductId_OccurredAt");

        builder.HasIndex(movement => new { movement.Source, movement.Reference, movement.ProductId })
            .IsUnique()
            .HasFilter("[Source] = 'PurchaseReceipt'")
            .HasDatabaseName("UX_InventoryMovements_PurchaseReceipt_Product");
    }
}
