using System.Linq.Expressions;
using System.Text.Json;
using Binesh.Ai.Orchestration;
using Binesh.Ai.QueryEngine;
using Binesh.Application.Abstractions;
using Binesh.Application.Features.Analytics.Shared;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Ai.Tools;

public sealed class DashboardAnalyticsTool(
    IBineshDbContext db,
    IBiAnalyticsService analytics,
    AiRequestContext requestContext) : IQueryableTool
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public string ToolName => "query_dashboard_widgets";
    public string Description => "List the current user's saved dashboards or execute a saved dashboard widget query.";
    public EntitySchema Schema { get; } = BuildSchema();

    public async Task<object> ExecuteAsync(AiQueryRequest request, CancellationToken cancellationToken)
    {
        if (requestContext.UserId is not Guid userId)
        {
            return new { error = "No authenticated user context is available." };
        }

        var dashboardId = ReadFilter(request, "dashboardId");
        var widgetId = ReadFilter(request, "widgetId");
        if (dashboardId is null)
        {
            var dashboards = await db.Dashboards
                .AsNoTracking()
                .Where(d => d.OwnerUserId == userId)
                .OrderByDescending(d => d.UpdatedAt)
                .Select(d => new { d.Id, d.Name, d.Description, d.Icon, d.UpdatedAt })
                .Take(request.Paging?.Take is > 0 ? request.Paging.Take : 20)
                .ToListAsync(cancellationToken);
            return dashboards;
        }

        if (!Guid.TryParse(dashboardId, out var id))
        {
            return new { error = "dashboardId must be a GUID." };
        }

        var dashboard = await db.Dashboards
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id && d.OwnerUserId == userId, cancellationToken);
        if (dashboard is null)
        {
            return new { error = "Dashboard not found." };
        }

        using var doc = JsonDocument.Parse(dashboard.ConfigJson);
        if (!doc.RootElement.TryGetProperty("widgets", out var widgets) || widgets.ValueKind != JsonValueKind.Array)
        {
            return new { dashboard.Id, dashboard.Name, widgets = Array.Empty<object>() };
        }

        var results = new List<object>();
        foreach (var widget in widgets.EnumerateArray())
        {
            var currentWidgetId = ReadString(widget, "id") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(widgetId)
                && !string.Equals(currentWidgetId, widgetId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!widget.TryGetProperty("query", out var queryEl) || queryEl.ValueKind != JsonValueKind.Object)
            {
                results.Add(new { widgetId = currentWidgetId, status = "skipped" });
                continue;
            }

            var query = queryEl.Deserialize<AnalyticsQueryRequest>(JsonOpts);
            if (query is null)
            {
                results.Add(new { widgetId = currentWidgetId, status = "skipped" });
                continue;
            }

            if (string.IsNullOrWhiteSpace(query.SourceId))
            {
                query = query with { SourceId = analytics.DefaultSourceId };
            }

            var rows = await analytics.ExecuteAsync(query, cancellationToken);
            results.Add(new { widgetId = currentWidgetId, status = "success", rows = rows.Rows });
        }

        return new { dashboard.Id, dashboard.Name, widgets = results };
    }

    private static string? ReadFilter(AiQueryRequest request, string field) =>
        request.Filters?
            .FirstOrDefault(f => string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase))
            ?.Value?.ToString();

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static EntitySchema BuildSchema() => new()
    {
        Name = "DashboardWidget",
        EntityType = typeof(DashboardToolRow),
        Fields =
        [
            StringField("dashboardId", x => x.DashboardId),
            StringField("widgetId", x => x.WidgetId),
            StringField("name", x => x.Name),
        ],
    };

    private static FieldDescriptor StringField(
        string name,
        Expression<Func<DashboardToolRow, string?>> selector) => new()
        {
            Name = name,
            Type = FieldType.String,
            Selector = selector,
            Groupable = true,
            AllowedOperators = ["eq", "ne"],
        };

    private sealed class DashboardToolRow
    {
        public string? DashboardId { get; set; }
        public string? WidgetId { get; set; }
        public string? Name { get; set; }
    }
}
