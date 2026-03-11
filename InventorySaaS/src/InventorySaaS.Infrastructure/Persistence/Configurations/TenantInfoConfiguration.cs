using InventorySaaS.Domain.Entities.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class TenantInfoConfiguration : IEntityTypeConfiguration<TenantInfo>
{
    public void Configure(EntityTypeBuilder<TenantInfo> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Slug)
            .IsUnique();

        builder.Property(t => t.Subdomain)
            .HasMaxLength(100);

        builder.HasIndex(t => t.Subdomain)
            .IsUnique()
            .HasFilter("[Subdomain] IS NOT NULL");

        builder.Property(t => t.Currency)
            .HasMaxLength(10);

        builder.Property(t => t.Timezone)
            .HasMaxLength(50);

        builder.HasOne(t => t.SubscriptionPlan)
            .WithMany(sp => sp.Tenants)
            .HasForeignKey(t => t.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Users)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.RowVersion)
            .IsRowVersion();
    }
}
