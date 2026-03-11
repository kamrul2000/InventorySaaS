using InventorySaaS.Domain.Entities.Supplier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class SupplierInfoConfiguration : IEntityTypeConfiguration<SupplierInfo>
{
    public void Configure(EntityTypeBuilder<SupplierInfo> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .HasMaxLength(200);

        builder.Property(s => s.Code)
            .HasMaxLength(50);

        builder.Property(s => s.Email)
            .HasMaxLength(256);

        builder.Property(s => s.RowVersion)
            .IsRowVersion();
    }
}
