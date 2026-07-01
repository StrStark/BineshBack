using Binesh.Domain.Chat;
using Binesh.Domain.Customers;
using Binesh.Domain.Financial;
using Binesh.Domain.Identity;
using Binesh.Domain.Products;
using Binesh.Domain.Sales;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Abstractions;

/// <summary>
/// Thin abstraction over the application DbContext. Implemented by
/// <c>Binesh.Infrastructure.Persistence.BineshDbContext</c>.
///
/// Handlers depend on this interface, never on the concrete DbContext, so
/// Application stays free of Infrastructure references. Add one DbSet line
/// here for each new aggregate root.
/// </summary>
public interface IBineshDbContext
{
    // Identity
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<IdentityUserRole<Guid>> UserRoles { get; }
    DbSet<UserSession> Sessions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    // Business
    DbSet<Customer> Customers { get; }
    DbSet<Person> Persons { get; }
    DbSet<Region> Regions { get; }
    DbSet<Product> Products { get; }
    DbSet<InventoryEvent> InventoryEvents { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SalesReturn> SalesReturns { get; }
    DbSet<FinancialEntry> FinancialEntries { get; }
    DbSet<FinancialMappingSettings> FinancialMappingSettings { get; }

    // Chat
    DbSet<Conversation> Conversations { get; }
    DbSet<ChatMessage> ChatMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
