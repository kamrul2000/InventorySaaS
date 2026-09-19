using InventorySaaS.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class ScanIdempotencyKeyConfiguration : IEntityTypeConfiguration<ScanIdempotencyKey>
{
    public void Configure(EntityTypeBuilder<ScanIdempotencyKey> builder)
    {
        builder.ToTable("ScanIdempotencyKeys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Key)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(k => k.Endpoint)
            .IsRequired()
            .HasMaxLength(200);

        // Unfiltered on purpose: a replayed key must collide even if the original row was
        // soft-deleted, otherwise the duplicate transaction it was meant to stop gets through.
        builder.HasIndex(k => new { k.TenantId, k.Key })
            .IsUnique();

        builder.Property(k => k.RowVersion)
            .IsRowVersion();
    }
}
