using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Binesh.Infrastructure.Persistence;

/// <summary>
/// Forces every DateTime read from / written to Postgres to be Kind=Utc.
/// Hoisted out of ApplicationDbContext from the old code so other contexts
/// can reuse it (and so it doesn't live as a nested type).
/// </summary>
public sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
