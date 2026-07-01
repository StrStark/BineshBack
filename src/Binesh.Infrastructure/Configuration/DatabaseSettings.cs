using System.ComponentModel.DataAnnotations;

namespace Binesh.Infrastructure.Configuration;

public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = default!;

    /// <summary>Retry up to N times on transient Npgsql errors (default 3).</summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>Command timeout in seconds (default 30).</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
