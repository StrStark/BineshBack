using Binesh.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.ProductCode).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ProductDescription).HasMaxLength(500).IsRequired();
        builder.Property(p => p.DetailedType).HasMaxLength(200);

        // ProductCode is the upstream SKU — unique across the catalogue.
        builder.HasIndex(p => p.ProductCode).IsUnique().HasDatabaseName("ix_products_code");
        builder.HasIndex(p => p.Type).HasDatabaseName("ix_products_type");

        builder.HasMany(p => p.Events)
               .WithOne()
               .HasForeignKey(e => e.ProductId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
