using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaGestion.Domain.Catalog.Products;
using SistemaGestion.Domain.Purchasing;

namespace SistemaGestion.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseLineConfiguration : IEntityTypeConfiguration<PurchaseLine>
{
    public void Configure(EntityTypeBuilder<PurchaseLine> builder)
    {
        builder.ToTable("PurchaseLines", table =>
        {
            table.HasCheckConstraint("CK_PurchaseLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_PurchaseLines_UnitCost_NonNegative", "[UnitCost] >= 0");
            table.HasCheckConstraint("CK_PurchaseLines_LineTotal_NonNegative", "[LineTotal] >= 0");
        });
        builder.HasKey(line => line.Id);
        builder.Property(line => line.PurchaseId).IsRequired();
        builder.Property(line => line.ProductId).IsRequired();
        builder.Property(line => line.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(line => line.UnitOfMeasure).HasMaxLength(50).IsRequired();
        builder.Property(line => line.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(line => line.UnitCost).HasPrecision(18, 4).IsRequired();
        builder.Property(line => line.LineTotal).HasPrecision(18, 4).IsRequired();
        builder.Property(line => line.LineTotal).HasField("_lineTotal");
        builder.HasIndex(line => new { line.PurchaseId, line.ProductId }).IsUnique()
            .HasDatabaseName("UX_PurchaseLines_Purchase_Product");
        builder.HasOne<Product>().WithMany().HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
