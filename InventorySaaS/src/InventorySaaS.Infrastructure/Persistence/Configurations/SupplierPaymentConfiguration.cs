using InventorySaaS.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> builder)
    {
        builder.ToTable("SupplierPayments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PaymentNumber).HasMaxLength(50);
        builder.HasIndex(p => new { p.TenantId, p.PaymentNumber }).IsUnique();

        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Reference).HasMaxLength(200);

        builder.HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Allocations)
            .WithOne(a => a.SupplierPayment)
            .HasForeignKey(a => a.SupplierPaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.RowVersion).IsRowVersion();
    }
}
