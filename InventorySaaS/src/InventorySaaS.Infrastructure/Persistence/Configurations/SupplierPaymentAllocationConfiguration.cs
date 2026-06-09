using InventorySaaS.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class SupplierPaymentAllocationConfiguration : IEntityTypeConfiguration<SupplierPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentAllocation> builder)
    {
        builder.ToTable("SupplierPaymentAllocations");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Amount).HasPrecision(18, 2);

        builder.HasOne(a => a.SupplierBill)
            .WithMany()
            .HasForeignKey(a => a.SupplierBillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.SupplierPaymentId, a.SupplierBillId });

        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}
