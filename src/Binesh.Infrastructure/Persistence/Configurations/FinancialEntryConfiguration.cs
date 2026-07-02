using Binesh.Domain.Financial;
using Binesh.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class FinancialEntryConfiguration : IEntityTypeConfiguration<FinancialEntry>
{
    public void Configure(EntityTypeBuilder<FinancialEntry> builder)
    {
        builder.ToTable("financial_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.CompanyId).IsRequired();

        builder.Property(e => e.Code).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Type).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Debit).HasColumnType("bigint").IsRequired();
        builder.Property(e => e.Credit).HasColumnType("bigint").IsRequired();

        builder.HasOne<Company>()
               .WithMany()
               .HasForeignKey(e => e.CompanyId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CompanyId, e.Code }).HasDatabaseName("ix_financial_entries_company_code");
        builder.HasIndex(e => new { e.CompanyId, e.Type }).HasDatabaseName("ix_financial_entries_company_type");
    }
}
