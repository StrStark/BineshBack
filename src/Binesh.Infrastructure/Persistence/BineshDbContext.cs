using Binesh.Application.Abstractions;
using Binesh.Domain.Ai;
using Binesh.Domain.Chat;
using Binesh.Domain.Customers;
using Binesh.Domain.Dashboards;
using Binesh.Domain.Financial;
using Binesh.Domain.Identity;
using Binesh.Domain.Products;
using Binesh.Domain.Sales;
using Binesh.Domain.Support;
using Binesh.Domain.Tenancy;
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
public class BineshDbContext(
    DbContextOptions<BineshDbContext> options,
    ITenantContext? tenantContext = null)
    : IdentityDbContext<User, Role, Guid>(options), IBineshDbContext
{
    public Guid? TenantCompanyId => tenantContext?.CompanyId;

    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<UserAiSettings> UserAiSettings => Set<UserAiSettings>();
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

        modelBuilder.Entity<Customer>().HasQueryFilter(e => TenantCompanyId == null || e.CompanyId == TenantCompanyId);
        modelBuilder.Entity<Product>().HasQueryFilter(e => TenantCompanyId == null || e.CompanyId == TenantCompanyId);
        modelBuilder.Entity<Sale>().HasQueryFilter(e => TenantCompanyId == null || e.CompanyId == TenantCompanyId);
        modelBuilder.Entity<SalesReturn>().HasQueryFilter(e => TenantCompanyId == null || e.CompanyId == TenantCompanyId);
        modelBuilder.Entity<FinancialEntry>().HasQueryFilter(e => TenantCompanyId == null || e.CompanyId == TenantCompanyId);
        modelBuilder.Entity<FinancialMappingSettings>().HasQueryFilter(e => TenantCompanyId == null || e.CompanyId == TenantCompanyId);

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
