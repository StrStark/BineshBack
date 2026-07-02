using Binesh.Domain.Identity;
using Binesh.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> builder)
    {
        builder.ToTable("support_ticket_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(m => m.Text).IsRequired().HasMaxLength(4000);
        builder.Property(m => m.Sender).IsRequired().HasMaxLength(32);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.TicketId, m.CreatedAt }).HasDatabaseName("ix_support_ticket_messages_ticket_created");
    }
}
