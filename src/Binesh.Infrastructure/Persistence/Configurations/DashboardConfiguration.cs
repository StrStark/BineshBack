using Binesh.Domain.Dashboards;
using Binesh.Domain.Identity;
using Binesh.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class DashboardConfiguration : IEntityTypeConfiguration<Dashboard>
{
    public void Configure(EntityTypeBuilder<Dashboard> builder)
    {
        builder.ToTable("dashboards");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(d => d.CompanyId).IsRequired();
        builder.Property(d => d.OwnerUserId).IsRequired();
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Description).HasMaxLength(1024);
        builder.Property(d => d.Icon).IsRequired().HasMaxLength(100);
        builder.Property(d => d.ConfigJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(d => new { d.CompanyId, d.OwnerUserId }).HasDatabaseName("ix_dashboards_company_owner");
        builder.HasIndex(d => d.UpdatedAt).HasDatabaseName("ix_dashboards_updated_at");
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
