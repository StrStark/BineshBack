using Binesh.Application.Abstractions;
using Binesh.Domain.Chat;
using Binesh.Domain.Customers;
using Binesh.Domain.Financial;
using Binesh.Domain.Identity;
using Binesh.Domain.Products;
using Binesh.Domain.Sales;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Infrastructure.Persistence;

/// <summary>
/// Single application DbContext — replaces the old two-context split.
/// Inherits <see cref="IdentityDbContext{TUser,TRole,TKey}"/> so the Identity
/// tables (AspNetUsers, AspNetRoles, ...) and our business tables live in one
/// schema and share one transaction scope.
///
/// Implements <see cref="IBineshDbContext"/> so handlers in the Application
/// layer can depend on the interface without referencing Infrastructure.
/// </summary>
public class BineshDbContext(DbContextOptions<BineshDbContext> options)
    : IdentityDbContext<User, Role, Guid>(options), IBineshDbContext
{
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryEvent> InventoryEvents => Set<InventoryEvent>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<FinancialEntry> FinancialEntries => Set<FinancialEntry>();
    public DbSet<FinancialMappingSettings> FinancialMappingSettings => Set<FinancialMappingSettings>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // IBineshDbContext disambiguation — IdentityDbContext exposes these as
    // protected/non-virtual members of the same name, so re-expose them.
    DbSet<IdentityUserRole<Guid>> IBineshDbContext.UserRoles => UserRoles;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pick up every IEntityTypeConfiguration<T> in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BineshDbContext).Assembly);

        // Every DateTime read from / written to Postgres is forced to UTC.
        // DateTimeOffset is left untouched (Npgsql handles it natively).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(new UtcDateTimeConverter());
                }
            }
        }
    }
}
