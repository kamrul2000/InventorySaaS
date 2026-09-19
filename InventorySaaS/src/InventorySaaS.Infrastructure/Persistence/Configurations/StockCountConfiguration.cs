using InventorySaaS.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class StockCountSessionConfiguration : IEntityTypeConfiguration<StockCountSession>
{
    public void Configure(EntityTypeBuilder<StockCountSession> builder)
    {
        builder.ToTable("StockCountSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.CountNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => new { s.TenantId, s.CountNumber })
            .IsUnique();

        builder.HasIndex(s => new { s.TenantId, s.WarehouseId, s.Status });

        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Location)
            .WithMany()
            .HasForeignKey(s => s.LocationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Lines)
            .WithOne(l => l.Session)
            .HasForeignKey(l => l.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.RowVersion)
            .IsRowVersion();
    }
}

public class StockCountLineConfiguration : IEntityTypeConfiguration<StockCountLine>
{
    public void Configure(EntityTypeBuilder<StockCountLine> builder)
    {
        builder.ToTable("StockCountLines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.BatchNumber)
            .HasMaxLength(100);

        // One line per product/location/batch within a session, so repeat scans of the same
        // combination accumulate onto a single row instead of fanning out.
        builder.HasIndex(l => new { l.SessionId, l.ProductId, l.LocationId, l.BatchNumber });

        builder.HasOne(l => l.Product)
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Location)
            .WithMany()
            .HasForeignKey(l => l.LocationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Serials)
            .WithOne(s => s.Line)
            .HasForeignKey(s => s.LineId)
            .OnDelete(DeleteBehavior.Cascade);

        // Computed in the domain from CountedQuantity - SystemQuantity; nothing to map.
        builder.Ignore(l => l.Variance);

        builder.Property(l => l.RowVersion)
            .IsRowVersion();
    }
}

public class StockCountLineSerialConfiguration : IEntityTypeConfiguration<StockCountLineSerial>
{
    public void Configure(EntityTypeBuilder<StockCountLineSerial> builder)
    {
        builder.ToTable("StockCountLineSerials");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SerialNumber)
            .IsRequired()
            .HasMaxLength(100);

        // A serial cannot be counted twice in the same line.
        builder.HasIndex(s => new { s.LineId, s.SerialNumber })
            .IsUnique();

        builder.Property(s => s.RowVersion)
            .IsRowVersion();
    }
}
