using Binesh.Domain.Products;
using Binesh.Domain.Tenancy;
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
        builder.Property(p => p.CompanyId).IsRequired();

        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.ProductCode).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ProductDescription).HasMaxLength(500).IsRequired();
        builder.Property(p => p.DetailedType).HasMaxLength(200);

        // ProductCode is the upstream SKU — unique across the catalogue.
        builder.HasIndex(p => new { p.CompanyId, p.ProductCode }).IsUnique().HasDatabaseName("ix_products_company_code");
        builder.HasIndex(p => new { p.CompanyId, p.Type }).HasDatabaseName("ix_products_company_type");

        builder.HasOne<Company>()
               .WithMany()
               .HasForeignKey(p => p.CompanyId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Events)
               .WithOne()
               .HasForeignKey(e => e.ProductId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
