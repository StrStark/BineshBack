namespace Binesh.Application.Features.Sales.Panel.Shared;

/// <summary>
/// Growth + time-bucket helpers ported verbatim from the legacy
/// <c>AppControllerBase</c> so panel numbers match the old app exactly.
/// </summary>
internal static class PanelMath
{
    /// <summary>
    /// Ratio of change: <c>(current - previous) / previous</c> rounded to 2 dp,
    /// or 0 when there is no prior period. NOTE: this is a ratio (e.g. 0.15),
    /// not a percentage — kept identical to the legacy behavior the frontend
    /// already renders.
    /// </summary>
    public static float CalculateGrowth(float current, float previous)
    {
        if (previous == 0) return 0;
        return (float)Math.Round((double)((current - previous) / previous), 2);
    }

    /// <summary>
    /// Coerces an incoming filter time to UTC so Npgsql can bind it against the
    /// <c>timestamptz</c> columns. Unspecified-kind values (e.g. a JSON time with
    /// no <c>Z</c>) are assumed to already be UTC; local times are converted.
    /// </summary>
    public static DateTime AsUtc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc),
    };

    /// <summary>
    /// Snaps a date to the start of its bucket. Week is Sunday-start, matching
    /// the legacy <c>date.AddDays(-(int)date.DayOfWeek)</c>.
    /// </summary>
    public static DateTime GetTimeFrameStart(DateTime date, TimeFrameUnit unit) => unit switch
    {
        TimeFrameUnit.Day => date.Date,
        TimeFrameUnit.Week => date.AddDays(-(int)date.DayOfWeek).Date,
        TimeFrameUnit.Month => new DateTime(date.Year, date.Month, 1),
        TimeFrameUnit.Quarter => new DateTime(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
        TimeFrameUnit.Year => new DateTime(date.Year, 1, 1),
        _ => date.Date,
    };
}
