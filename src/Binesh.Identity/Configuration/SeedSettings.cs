namespace Binesh.Identity.Configuration;

/// <summary>
/// Bootstrap settings. The hosted seeder reads <c>Seed:SuperAdmin</c> on
/// every startup and ensures exactly one SuperAdmin exists.
///
/// If <see cref="SuperAdmin"/>.PhoneNumber is missing, the seeder is a no-op
/// — useful in tests and CI. In prod, the env var MUST be set on first boot
/// so the system has at least one administrator.
/// </summary>
public sealed class SeedSettings
{
    public const string SectionName = "Seed";

    public SuperAdminSeed SuperAdmin { get; set; } = new();
    public CompanySeed Company { get; set; } = new();

    /// <summary>
    /// When <c>true</c>, the dev/demo data seeder populates products, customers,
    /// sales, and returns via the CRUD command handlers on startup (idempotent —
    /// skips if sales already exist). Off by default; never enable in prod.
    /// </summary>
    public bool InitialData { get; set; }

    public sealed class SuperAdminSeed
    {
        public string? PhoneNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public sealed class CompanySeed
    {
        public string Name { get; set; } = "Binesh";
        public string Slug { get; set; } = "binesh";
    }
}
