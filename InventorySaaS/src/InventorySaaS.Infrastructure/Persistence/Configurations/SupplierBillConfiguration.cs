using InventorySaaS.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class SupplierBillConfiguration : IEntityTypeConfiguration<SupplierBill>
{
    public void Configure(EntityTypeBuilder<SupplierBill> builder)
    {
        builder.ToTable("SupplierBills");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BillNumber).HasMaxLength(50);
        builder.Property(b => b.SupplierInvoiceNumber).HasMaxLength(100);

        builder.HasIndex(b => new { b.TenantId, b.BillNumber }).IsUnique();

        builder.Property(b => b.SubTotal).HasPrecision(18, 2);
        builder.Property(b => b.TaxAmount).HasPrecision(18, 2);
        builder.Property(b => b.DiscountAmount).HasPrecision(18, 2);
        builder.Property(b => b.TotalAmount).HasPrecision(18, 2);
        builder.Property(b => b.AmountPaid).HasPrecision(18, 2);

        builder.Ignore(b => b.BalanceDue);

        builder.HasOne(b => b.Supplier)
            .WithMany()
            .HasForeignKey(b => b.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.PurchaseOrder)
            .WithMany()
            .HasForeignKey(b => b.PurchaseOrderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Items)
            .WithOne(it => it.SupplierBill)
            .HasForeignKey(it => it.SupplierBillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(b => b.RowVersion).IsRowVersion();
    }
}
