using InventorySaaS.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class ProductSerialConfiguration : IEntityTypeConfiguration<ProductSerial>
{
    public void Configure(EntityTypeBuilder<ProductSerial> builder)
    {
        builder.ToTable("ProductSerials");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SerialNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.BatchNumber)
            .HasMaxLength(100);

        builder.Property(s => s.UnitCost)
            .HasPrecision(18, 2);

        // "No duplicate serial numbers within a tenant" enforced by the database, not by a
        // service-level check two concurrent scans could both pass. Filtered so soft-deleted
        // rows free their serial for reuse.
        builder.HasIndex(s => new { s.TenantId, s.SerialNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(s => new { s.TenantId, s.ProductId, s.Status });

        builder.HasOne(s => s.Product)
            .WithMany(p => p.Serials)
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Location)
            .WithMany()
            .HasForeignKey(s => s.LocationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(s => s.RowVersion)
            .IsRowVersion();
    }
}
