using InventorySaaS.Domain.Entities.Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventorySaaS.Infrastructure.Persistence.Configurations;

public class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("UnitsOfMeasure");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .HasMaxLength(100);

        builder.Property(u => u.Abbreviation)
            .HasMaxLength(20);

        // Matches Category/Brand's (non-unique) name index - lookups and the name-search list
        // query were doing a full table scan without this (DATA-03).
        builder.HasIndex(u => new { u.TenantId, u.Name });

        builder.Property(u => u.RowVersion)
            .IsRowVersion();
    }
}
