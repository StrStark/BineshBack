using Binesh.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class InventoryEventConfiguration : IEntityTypeConfiguration<InventoryEvent>
{
    public void Configure(EntityTypeBuilder<InventoryEvent> builder)
    {
        builder.ToTable("inventory_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.UnitPrice).HasColumnType("bigint");
        builder.Property(e => e.TotalPrice).HasColumnType("bigint");

        builder.Property(e => e.Value1).HasMaxLength(200);
        builder.Property(e => e.Value2).HasMaxLength(200);
        builder.Property(e => e.Value3).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(500);

        // Most stagnation / aggregation queries filter by product + date.
        builder.HasIndex(e => new { e.ProductId, e.Date })
               .HasDatabaseName("ix_inventory_events_product_date");
    }
}
