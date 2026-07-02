using Binesh.Domain.Ai;
using Binesh.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class UserAiSettingsConfiguration : IEntityTypeConfiguration<UserAiSettings>
{
    public void Configure(EntityTypeBuilder<UserAiSettings> builder)
    {
        builder.ToTable("user_ai_settings");
        builder.HasKey(s => s.UserId);
        builder.Property(s => s.ApiKeyEncrypted).HasMaxLength(4096);
        builder.Property(s => s.ApiKeyPreview).HasMaxLength(32);
        builder.Property(s => s.Model).HasMaxLength(128);
        builder.Property(s => s.BaseUrl).HasMaxLength(512);
        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserAiSettings>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
