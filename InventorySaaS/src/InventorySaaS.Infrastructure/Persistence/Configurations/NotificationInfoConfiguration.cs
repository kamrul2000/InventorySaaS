using InventorySaaS.Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class NotificationInfoConfiguration : IEntityTypeConfiguration<NotificationInfo>
{
    public void Configure(EntityTypeBuilder<NotificationInfo> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .HasMaxLength(1000);

        builder.Property(n => n.RowVersion)
            .IsRowVersion();
    }
}
