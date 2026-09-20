using InventorySaaS.Domain.Entities.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class WarehouseLocationConfiguration : IEntityTypeConfiguration<WarehouseLocation>
{
    public void Configure(EntityTypeBuilder<WarehouseLocation> builder)
    {
        builder.ToTable("WarehouseLocations");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .HasMaxLength(200);

        builder.Property(l => l.Code)
            .HasMaxLength(50);

        builder.Property(l => l.Barcode)
            .HasMaxLength(100);

        // Both are optional, but each must resolve to one bin when a location label is scanned.
        builder.HasIndex(l => new { l.TenantId, l.Code })
            .IsUnique()
            .HasFilter("[Code] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(l => new { l.TenantId, l.Barcode })
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(l => l.Warehouse)
            .WithMany(w => w.Locations)
            .HasForeignKey(l => l.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(l => l.RowVersion)
            .IsRowVersion();
    }
}
