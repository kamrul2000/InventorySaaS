using InventorySaaS.Domain.Entities.Purchase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class PurchaseRequisitionConfiguration : IEntityTypeConfiguration<PurchaseRequisition>
{
    public void Configure(EntityTypeBuilder<PurchaseRequisition> builder)
    {
        builder.ToTable("PurchaseRequisitions");

        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.RequisitionNumber)
            .HasMaxLength(50);

        builder.HasMany(pr => pr.Items)
            .WithOne(i => i.Requisition)
            .HasForeignKey(i => i.RequisitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(pr => pr.RowVersion)
            .IsRowVersion();
    }
}
