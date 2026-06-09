using InventorySaaS.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class SupplierBillItemConfiguration : IEntityTypeConfiguration<SupplierBillItem>
{
    public void Configure(EntityTypeBuilder<SupplierBillItem> builder)
    {
        builder.ToTable("SupplierBillItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Description).HasMaxLength(500);

        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Property(i => i.TaxRate).HasPrecision(18, 2);
        builder.Property(i => i.DiscountRate).HasPrecision(18, 2);
        builder.Property(i => i.LineTotal).HasPrecision(18, 2);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.RowVersion).IsRowVersion();
    }
}
