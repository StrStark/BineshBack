using Binesh.Domain.Customers;
using Binesh.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Binesh.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.CompanyId).IsRequired();

        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Active).IsRequired();
        builder.Property(c => c.PaymentReliability).IsRequired();

        // 1:1 with Person; deleting a customer deletes the Person row too
        // (Person isn't reused across customers in this model).
        builder.HasOne(c => c.Person)
               .WithMany()
               .HasForeignKey(c => c.PersonId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>()
               .WithMany()
               .HasForeignKey(c => c.CompanyId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.CompanyId, c.Type }).HasDatabaseName("ix_customers_company_type");
        builder.HasIndex(c => new { c.CompanyId, c.Active }).HasDatabaseName("ix_customers_company_active");
    }
}
