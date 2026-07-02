using Binesh.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(120);
        builder.Property(c => c.Domain).HasMaxLength(255);
        builder.Property(c => c.Logo).HasMaxLength(512);
        builder.Property(c => c.Phone).HasMaxLength(64);
        builder.Property(c => c.Email).HasMaxLength(255);
        builder.Property(c => c.Address).HasMaxLength(1024);
        builder.Property(c => c.Status).IsRequired().HasMaxLength(32);
        builder.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("ix_companies_slug");
    }
}
