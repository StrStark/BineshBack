using Binesh.Domain.Identity;
using Binesh.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.JobTitle).HasMaxLength(150);
        builder.Property(u => u.ProfileImageName).HasMaxLength(255);
        builder.Property(u => u.CompanyId);
        builder.HasIndex(u => u.CompanyId).HasDatabaseName("ix_users_company_id");
        builder.HasOne<Company>()
               .WithMany()
               .HasForeignKey(u => u.CompanyId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Sessions)
               .WithOne(s => s.User)
               .HasForeignKey(s => s.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
