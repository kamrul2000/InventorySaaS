using InventorySaaS.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        builder.ToTable("InventoryBalances");

        builder.HasKey(ib => ib.Id);

        builder.HasIndex(ib => new { ib.TenantId, ib.ProductId, ib.WarehouseId, ib.LocationId, ib.BatchNumber });

        builder.Property(ib => ib.UnitCost)
            .HasPrecision(18, 2);

        builder.HasOne(ib => ib.Product)
            .WithMany(p => p.InventoryBalances)
            .HasForeignKey(ib => ib.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ib => ib.Warehouse)
            .WithMany(w => w.InventoryBalances)
            .HasForeignKey(ib => ib.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ib => ib.Location)
            .WithMany(l => l.InventoryBalances)
            .HasForeignKey(ib => ib.LocationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Ignore(ib => ib.QuantityAvailable);

        builder.Property(ib => ib.RowVersion)
            .IsRowVersion();
    }
}
