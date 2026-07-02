using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;

namespace Binesh.Infrastructure.Configuration;

public sealed class BiSourceSettings
{
    public const string SectionName = "BiSources";

    public SqlServerSourceSettings DefaultSqlServer { get; set; } = new();
}

public sealed class SqlServerSourceSettings
{
    public string Id { get; set; } = "default-sqlserver";
    public string Name { get; set; } = "Anbar + Hesab";
    public bool Enabled { get; set; } = true;

    [Required]
    public string Host { get; set; } = "host.docker.internal";

    public string? DataSource { get; set; }

    public int Port { get; set; } = 1433;

    [Required]
    public string Username { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;

    public string DefaultDatabase { get; set; } = "Anbar";
    public string AnbarDatabase { get; set; } = "Anbar";
    public string HesabDatabase { get; set; } = "Hesab";
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public bool PersistSecurityInfo { get; set; }
    public bool Pooling { get; set; } = true;
    public string ApplicationName { get; set; } = "Binesh.Api";
    public int CommandTimeout { get; set; }

    public string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(DataSource) ? $"{Host},{Port}" : DataSource,
            InitialCatalog = DefaultDatabase,
            UserID = Username,
            Password = Password,
            Encrypt = Encrypt,
            TrustServerCertificate = TrustServerCertificate,
            PersistSecurityInfo = PersistSecurityInfo,
            MultipleActiveResultSets = false,
            Pooling = Pooling,
            ConnectTimeout = 15,
        };

        if (!string.IsNullOrWhiteSpace(ApplicationName))
        {
            builder.ApplicationName = ApplicationName;
        }

        return builder.ConnectionString;
    }
}
