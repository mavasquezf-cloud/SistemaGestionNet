using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SistemaGestion.Domain.Catalog.Categories;
using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        var skuConverter = new ValueConverter<Sku, string>(
            sku => sku.Value,
            value => new Sku(value));

        builder.ToTable("Products");
        builder.HasKey(product => product.Id);

        builder.Property(product => product.Sku)
            .HasConversion(skuConverter)
            .HasColumnName("Sku")
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(product => product.Sku)
            .IsUnique()
            .HasDatabaseName("IX_Products_Sku");

        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(1000);

        builder.Property(product => product.CategoryId)
            .IsRequired();

        builder.Property(product => product.UnitOfMeasure)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(product => product.DefaultSalePrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(product => product.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
