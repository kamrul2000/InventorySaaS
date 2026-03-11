using InventorySaaS.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");

        builder.HasKey(so => so.Id);

        builder.Property(so => so.OrderNumber)
            .HasMaxLength(50);

        builder.HasIndex(so => new { so.TenantId, so.OrderNumber })
            .IsUnique();

        builder.Property(so => so.SubTotal)
            .HasPrecision(18, 2);

        builder.Property(so => so.TaxAmount)
            .HasPrecision(18, 2);

        builder.Property(so => so.DiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(so => so.TotalAmount)
            .HasPrecision(18, 2);

        builder.HasOne(so => so.Customer)
            .WithMany(c => c.SalesOrders)
            .HasForeignKey(so => so.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(so => so.Warehouse)
            .WithMany()
            .HasForeignKey(so => so.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(so => so.Items)
            .WithOne(i => i.SalesOrder)
            .HasForeignKey(i => i.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(so => so.RowVersion)
            .IsRowVersion();
    }
}
