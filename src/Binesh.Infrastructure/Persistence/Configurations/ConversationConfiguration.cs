using Binesh.Domain.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.Title).IsRequired().HasMaxLength(256);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ArchivedAt);

        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        var nav = builder.Metadata.FindNavigation(nameof(Conversation.Messages))!;
        nav.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.UserId).HasDatabaseName("ix_conversations_user");
        builder.HasIndex(c => new { c.UserId, c.ArchivedAt }).HasDatabaseName("ix_conversations_user_archived");
    }
}
