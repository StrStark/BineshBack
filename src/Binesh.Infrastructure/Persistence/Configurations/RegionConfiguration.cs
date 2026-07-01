using Binesh.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("regions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.Country).HasMaxLength(100);
        builder.Property(r => r.Province).HasMaxLength(100);
        builder.Property(r => r.City).HasMaxLength(100);

        // Unique tuple — one row per geographic location, reused across persons.
        builder.HasIndex(r => new { r.Country, r.Province, r.City })
               .HasDatabaseName("ix_regions_country_province_city")
               .IsUnique();
    }
}
