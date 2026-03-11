using InventorySaaS.Domain.Entities.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class WarehouseInfoConfiguration : IEntityTypeConfiguration<WarehouseInfo>
{
    public void Configure(EntityTypeBuilder<WarehouseInfo> builder)
    {
        builder.ToTable("Warehouses");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .HasMaxLength(200);

        builder.Property(w => w.Code)
            .HasMaxLength(50);

        builder.HasIndex(w => new { w.TenantId, w.Code })
            .IsUnique();

        builder.Property(w => w.RowVersion)
            .IsRowVersion();
    }
}
