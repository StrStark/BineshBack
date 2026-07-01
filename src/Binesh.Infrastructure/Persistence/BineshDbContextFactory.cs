using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Binesh.Infrastructure.Persistence;

/// <summary>
/// Used by EF Core CLI (dotnet ef migrations add ...) so it can construct a
/// DbContext without booting the full host. Reads connection string from the
/// CONN_STRING env var.
/// </summary>
public sealed class BineshDbContextFactory : IDesignTimeDbContextFactory<BineshDbContext>
{
    public BineshDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("CONN_STRING")
            ?? "Host=localhost;Port=5432;Database=binesh;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<BineshDbContext>()
            .UseNpgsql(conn, npg => npg.MigrationsAssembly(typeof(BineshDbContext).Assembly.GetName().Name))
            .Options;

        return new BineshDbContext(options);
    }
}
