using Binesh.Domain.Support;
using Binesh.Domain.Identity;
using Binesh.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("support_tickets");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(256);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(4000);
        builder.Property(t => t.Status).IsRequired().HasMaxLength(32);
        builder.Property(t => t.Priority).IsRequired().HasMaxLength(32);
        builder.Property(t => t.CompanyId).IsRequired();
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(t => t.Messages)
            .WithOne()
            .HasForeignKey(m => m.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => new { t.CompanyId, t.Status }).HasDatabaseName("ix_support_tickets_company_status");
        builder.HasIndex(t => t.AccountId).HasDatabaseName("ix_support_tickets_account");
    }
}
