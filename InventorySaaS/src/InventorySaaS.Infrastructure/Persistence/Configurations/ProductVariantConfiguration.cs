using InventorySaaS.Domain.Entities.Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name)
            .HasMaxLength(200);

        builder.Property(v => v.Sku)
            .HasMaxLength(50);

        builder.Property(v => v.Barcode)
            .HasMaxLength(100);

        // Variant barcodes share the product barcode namespace from the scanner's point of
        // view, but uniqueness is enforced per table; ScanService checks across both.
        builder.HasIndex(v => new { v.TenantId, v.Barcode })
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(v => v.RowVersion)
            .IsRowVersion();
    }
}
