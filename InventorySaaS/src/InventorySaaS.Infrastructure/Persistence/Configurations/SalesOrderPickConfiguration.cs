using InventorySaaS.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class SalesOrderPickSessionConfiguration : IEntityTypeConfiguration<SalesOrderPickSession>
{
    public void Configure(EntityTypeBuilder<SalesOrderPickSession> builder)
    {
        builder.ToTable("SalesOrderPickSessions");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.TenantId, s.SalesOrderId, s.Status });

        builder.Property(s => s.PickedBy)
            .HasMaxLength(256);

        builder.HasOne(s => s.SalesOrder)
            .WithMany()
            .HasForeignKey(s => s.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Events)
            .WithOne(e => e.Session)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.RowVersion)
            .IsRowVersion();
    }
}

public class SalesOrderPickEventConfiguration : IEntityTypeConfiguration<SalesOrderPickEvent>
{
    public void Configure(EntityTypeBuilder<SalesOrderPickEvent> builder)
    {
        builder.ToTable("SalesOrderPickEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.BatchNumber)
            .HasMaxLength(100);

        builder.Property(e => e.SerialNumber)
            .HasMaxLength(100);

        builder.HasIndex(e => new { e.SessionId, e.ProductId });

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.RowVersion)
            .IsRowVersion();
    }
}
