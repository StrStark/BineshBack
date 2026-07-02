using Binesh.Domain.Sales;
using Binesh.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("sales_returns");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.CompanyId).IsRequired();

        builder.Property(s => s.Date).IsRequired();
        builder.Property(s => s.Price).HasColumnType("bigint").IsRequired();
        builder.Property(s => s.Quantity).IsRequired();
        builder.Property(s => s.DeliveredQuantity).IsRequired();
        builder.Property(s => s.DocNumber).IsRequired();

        builder.HasOne(s => s.Product)
            .WithMany()
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Counterparty)
            .WithMany()
            .HasForeignKey(s => s.CounterpartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.CompanyId, s.Date }).HasDatabaseName("ix_sales_returns_company_date");
        builder.HasIndex(s => new { s.CompanyId, s.ProductId }).HasDatabaseName("ix_sales_returns_company_product");
        builder.HasIndex(s => new { s.CompanyId, s.CounterpartyId }).HasDatabaseName("ix_sales_returns_company_counterparty");
    }
}
