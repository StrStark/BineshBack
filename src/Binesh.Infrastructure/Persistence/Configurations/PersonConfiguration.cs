using Binesh.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("persons");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Family).HasMaxLength(150);
        builder.Property(p => p.Code).HasMaxLength(50);
        builder.Property(p => p.Phone).HasMaxLength(50);
        builder.Property(p => p.Fax).HasMaxLength(50);
        builder.Property(p => p.Mobile).HasMaxLength(30);
        builder.Property(p => p.Pelak).HasMaxLength(50);
        builder.Property(p => p.Address).HasMaxLength(500);

        builder.HasOne(p => p.Region)
               .WithMany()
               .HasForeignKey(p => p.RegionId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.Code).HasDatabaseName("ix_persons_code");
    }
}
