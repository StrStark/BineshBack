using Binesh.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("sales");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

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

        builder.HasIndex(s => s.Date).HasDatabaseName("ix_sales_date");
        builder.HasIndex(s => s.ProductId).HasDatabaseName("ix_sales_product");
        builder.HasIndex(s => s.CounterpartyId).HasDatabaseName("ix_sales_counterparty");
    }
}
