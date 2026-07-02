using System.Security.Claims;
using System.Text.Json;
using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Analytics.Shared;
using Binesh.Domain.Dashboards;
using Binesh.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Api.Endpoints.Dashboards;

public static class DashboardEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> FilterOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "equals",
        "eq",
        "not_equals",
        "notequals",
        "ne",
        "not",
        "contains",
        "not_contains",
        "notcontains",
        "gt",
        "greater",
        "greater_than",
        "greaterthan",
        "gte",
        "ge",
        "greatereq",
        "greater_than_or_equal",
        "greaterthanorequal",
        "lt",
        "less",
        "less_than",
        "lessthan",
        "lte",
        "le",
        "lesseq",
        "less_than_or_equal",
        "lessthanorequal",
    };

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dashboards")
            .WithTags("Dashboards")
            .RequireAuthorization();

        group.MapGet("/", ListDashboards).WithName(nameof(ListDashboards));
        group.MapPost("/", CreateDashboard).WithName(nameof(CreateDashboard));
        group.MapGet("/{id:guid}", GetDashboard).WithName(nameof(GetDashboard));
        group.MapPut("/{id:guid}", UpdateDashboard).WithName(nameof(UpdateDashboard));
        group.MapDelete("/{id:guid}", DeleteDashboard).WithName(nameof(DeleteDashboard));
        group.MapPost("/{id:guid}/render", RenderDashboard).WithName(nameof(RenderDashboard));

        return routes;
    }

    private static async Task<IResult> ListDashboards(
        Guid? companyId,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var userId = RequireUserId(user);
        var isSuperAdmin = user.IsInRole(AppRoles.SuperAdmin);

        var query = db.Dashboards.AsNoTracking();
        if (isSuperAdmin)
        {
            if (companyId is not null)
            {
                query = query.Where(d => d.CompanyId == companyId);
            }
        }
        else
        {
            var userCompanyId = await ResolveCompanyIdAsync(user, db, null, ct)
                ?? throw new ForbiddenException();
            if (companyId is not null && companyId != userCompanyId)
            {
                throw new ForbiddenException();
            }
            query = query.Where(d => d.CompanyId == userCompanyId && d.OwnerUserId == userId);
        }

        var items = await query
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new DashboardListItem(d.Id, d.Name, d.Description, d.Icon, d.CreatedAt, d.UpdatedAt))
            .ToListAsync(ct);

        return Results.Ok(ApiEnvelope.Success(items));
    }

    private static async Task<IResult> CreateDashboard(
        [FromBody] DashboardSaveRequest body,
        ClaimsPrincipal user,
        IBineshDbContext db,
        IBiAnalyticsService analytics,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(nameof(body.Name), "Name is required.")
            ]);
        }

        var companyId = await ResolveCompanyIdAsync(user, db, body.CompanyId, ct)
            ?? throw new ConflictException("A company is required before dashboards can be created.");
        var userId = RequireUserId(user);
        var dashboard = new Dashboard
        {
            CompanyId = companyId,
            OwnerUserId = userId,
            Name = body.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim(),
            Icon = string.IsNullOrWhiteSpace(body.Icon) ? "LayoutDashboard" : body.Icon.Trim(),
            ConfigJson = ValidateAndNormalizeConfig(body.Config, analytics),
        };

        db.Dashboards.Add(dashboard);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dashboards/{dashboard.Id}", ApiEnvelope.Success(ToDetails(dashboard)));
    }

    private static async Task<IResult> GetDashboard(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var dashboard = await LoadAuthorizedDashboardAsync(id, user, db, ct);
        return Results.Ok(ApiEnvelope.Success(ToDetails(dashboard)));
    }

    private static async Task<IResult> UpdateDashboard(
        Guid id,
        [FromBody] DashboardSaveRequest body,
        ClaimsPrincipal user,
        IBineshDbContext db,
        IBiAnalyticsService analytics,
        CancellationToken ct)
    {
        var dashboard = await LoadAuthorizedDashboardAsync(id, user, db, ct, tracking: true);
        if (!string.IsNullOrWhiteSpace(body.Name)) { dashboard.Name = body.Name.Trim(); }
        dashboard.Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
        if (!string.IsNullOrWhiteSpace(body.Icon)) { dashboard.Icon = body.Icon.Trim(); }
        if (body.Config is not null) { dashboard.ConfigJson = ValidateAndNormalizeConfig(body.Config, analytics); }
        dashboard.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ApiEnvelope.Success(ToDetails(dashboard)));
    }

    private static async Task<IResult> DeleteDashboard(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var dashboard = await LoadAuthorizedDashboardAsync(id, user, db, ct, tracking: true);
        db.Dashboards.Remove(dashboard);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RenderDashboard(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        IBiAnalyticsService analytics,
        CancellationToken ct)
    {
        var dashboard = await LoadAuthorizedDashboardAsync(id, user, db, ct);
        var config = JsonSerializer.Deserialize<JsonElement>(dashboard.ConfigJson, JsonOpts);
        var widgets = new List<object>();

        if (config.ValueKind == JsonValueKind.Object
            && config.TryGetProperty("widgets", out var widgetsEl)
            && widgetsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var widget in widgetsEl.EnumerateArray())
            {
                var widgetId = ReadString(widget, "id") ?? Guid.NewGuid().ToString("N");
                try
                {
                    var request = BuildWidgetQuery(widget, analytics);
                    if (request is null)
                    {
                        widgets.Add(new { widgetId, status = "skipped", rows = Array.Empty<object>() });
                        continue;
                    }
                    var result = await analytics.ExecuteAsync(request, ct);
                    widgets.Add(new { widgetId, status = "success", rows = result.Rows });
                }
                catch (Exception ex)
                {
                    widgets.Add(new { widgetId, status = "error", message = ex.Message, rows = Array.Empty<object>() });
                }
            }
        }

        return Results.Ok(ApiEnvelope.Success(new { dashboardId = id, widgets }));
    }

    private static AnalyticsQueryRequest? BuildWidgetQuery(JsonElement widget, IBiAnalyticsService analytics)
    {
        if (widget.TryGetProperty("query", out var queryEl) && queryEl.ValueKind == JsonValueKind.Object)
        {
            var fromQuery = queryEl.Deserialize<AnalyticsQueryRequest>(JsonOpts);
            if (fromQuery is not null)
            {
                return NormalizeQuery(fromQuery, analytics);
            }
        }

        var dataset = ReadString(widget, "datasetId") ?? ReadString(widget, "table");
        if (string.IsNullOrWhiteSpace(dataset)) { return null; }

        return new AnalyticsQueryRequest(
            analytics.DefaultSourceId,
            dataset,
            ReadString(widget, "labelField"),
            ReadString(widget, "valueField"),
            ReadString(widget, "aggregation"),
            null,
            null,
            null,
            null,
            ReadInt(widget, "limit"),
            Raw: false);
    }

    private static async Task<Dashboard> LoadAuthorizedDashboardAsync(
        Guid id,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct,
        bool tracking = false)
    {
        var query = tracking ? db.Dashboards : db.Dashboards.AsNoTracking();
        var dashboard = await query.SingleOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Dashboard", id);

        var userId = RequireUserId(user);
        var companyId = await ResolveCompanyIdAsync(user, db, dashboard.CompanyId, ct);
        var isSuperAdmin = user.IsInRole(AppRoles.SuperAdmin);
        if (!isSuperAdmin && (dashboard.CompanyId != companyId || dashboard.OwnerUserId != userId))
        {
            throw new ForbiddenException();
        }

        return dashboard;
    }

    private static string ValidateAndNormalizeConfig(JsonElement? config, IBiAnalyticsService analytics)
    {
        if (config is null || config.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return """{"widgets":[]}""";
        }
        if (config.Value.ValueKind != JsonValueKind.Object)
        {
            throw new ConflictException("Dashboard config must be a JSON object.", "dashboard.invalid_config");
        }

        ValidateDashboardConfig(config.Value, analytics);
        return config.Value.GetRawText();
    }

    private static void ValidateDashboardConfig(JsonElement config, IBiAnalyticsService analytics)
    {
        if (!config.TryGetProperty("widgets", out var widgets))
        {
            return;
        }
        if (widgets.ValueKind != JsonValueKind.Array)
        {
            throw new ConflictException("Dashboard config widgets must be an array.", "dashboard.invalid_config");
        }

        foreach (var widget in widgets.EnumerateArray())
        {
            if (widget.ValueKind != JsonValueKind.Object)
            {
                throw new ConflictException("Dashboard widget config must be a JSON object.", "dashboard.invalid_config");
            }

            var request = BuildWidgetQuery(widget, analytics);
            if (request is null)
            {
                continue;
            }

            ValidateQueryConfig(request, analytics, ReadString(widget, "id") ?? "widget");
        }
    }

    private static void ValidateQueryConfig(
        AnalyticsQueryRequest request,
        IBiAnalyticsService analytics,
        string widgetId)
    {
        request = NormalizeQuery(request, analytics);
        var sourceId = string.IsNullOrWhiteSpace(request.SourceId)
            ? analytics.DefaultSourceId
            : request.SourceId;
        var datasetId = FirstNonBlank(request.DatasetId, request.Table);
        var schema = analytics.GetSchema(sourceId);
        var dataset = schema.Datasets.SingleOrDefault(d =>
                string.Equals(d.Id, datasetId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConflictException($"Dashboard widget '{widgetId}' references unknown dataset '{datasetId}'.", "dashboard.invalid_dataset");
        var fields = dataset.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        ValidateField(fields, request.LabelField, widgetId, "labelField");
        ValidateField(fields, request.ValueField, widgetId, "valueField", request.Aggregation);

        foreach (var groupField in request.GroupBy ?? [])
        {
            ValidateField(fields, groupField, widgetId, "groupBy");
        }

        foreach (var value in request.Values ?? [])
        {
            ValidateField(fields, value.Field, widgetId, "values", value.Aggregation);
        }

        foreach (var filter in request.Filters ?? [])
        {
            ValidateField(fields, filter.Field, widgetId, "filters");
            if (string.IsNullOrWhiteSpace(filter.Operator) || !FilterOperators.Contains(filter.Operator))
            {
                throw new ConflictException($"Dashboard widget '{widgetId}' uses unsupported filter operator '{filter.Operator}'.", "dashboard.invalid_filter");
            }
        }

        if (request.OrderBy is not null && !string.IsNullOrWhiteSpace(request.OrderBy.Field))
        {
            var aliases = (request.Values ?? [])
                .Select(v => v.Alias)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!fields.ContainsKey(request.OrderBy.Field) && !aliases.Contains(request.OrderBy.Field))
            {
                throw new ConflictException($"Dashboard widget '{widgetId}' orders by unknown field '{request.OrderBy.Field}'.", "dashboard.invalid_order");
            }
        }
    }

    private static AnalyticsQueryRequest NormalizeQuery(
        AnalyticsQueryRequest request,
        IBiAnalyticsService analytics)
    {
        var sourceId = string.IsNullOrWhiteSpace(request.SourceId)
            ? analytics.DefaultSourceId
            : NormalizeDatasetAlias(request.SourceId);
        var datasetId = NormalizeDatasetAlias(FirstNonBlank(request.DatasetId, request.Table));

        if (string.IsNullOrWhiteSpace(datasetId)
            && !string.Equals(sourceId, analytics.DefaultSourceId, StringComparison.OrdinalIgnoreCase))
        {
            datasetId = sourceId;
            sourceId = analytics.DefaultSourceId;
        }

        return request with
        {
            SourceId = sourceId,
            DatasetId = datasetId,
        };
    }

    private static string? NormalizeDatasetAlias(string? datasetId) =>
        string.Equals(datasetId, "FinancialTransaction", StringComparison.OrdinalIgnoreCase)
            ? "Financial"
            : datasetId;

    private static void ValidateField(
        IReadOnlyDictionary<string, BiFieldDto> fields,
        string? field,
        string widgetId,
        string context,
        string? aggregation = null)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return;
        }
        if (!fields.TryGetValue(field, out var definition))
        {
            throw new ConflictException($"Dashboard widget '{widgetId}' references unknown {context} field '{field}'.", "dashboard.invalid_field");
        }
        if (!string.IsNullOrWhiteSpace(aggregation)
            && !definition.AllowedAggregations.Contains(aggregation, StringComparer.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Dashboard widget '{widgetId}' cannot use aggregation '{aggregation}' on field '{field}'.", "dashboard.invalid_aggregation");
        }
    }

    private static DashboardDetails ToDetails(Dashboard dashboard) => new(
        dashboard.Id,
        dashboard.CompanyId,
        dashboard.OwnerUserId,
        dashboard.Name,
        dashboard.Description,
        dashboard.Icon,
        JsonSerializer.Deserialize<JsonElement>(dashboard.ConfigJson, JsonOpts),
        dashboard.CreatedAt,
        dashboard.UpdatedAt);

    private static async Task<Guid?> ResolveCompanyIdAsync(
        ClaimsPrincipal user,
        IBineshDbContext db,
        Guid? requestedCompanyId,
        CancellationToken ct)
    {
        var isSuperAdmin = user.IsInRole(AppRoles.SuperAdmin);
        if (isSuperAdmin && requestedCompanyId is not null) { return requestedCompanyId; }

        var userId = RequireUserId(user);
        var companyId = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CompanyId)
            .SingleAsync(ct);

        return companyId ?? (isSuperAdmin ? requestedCompanyId : null);
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."));

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var i)
            ? i
            : null;

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    public sealed record DashboardSaveRequest(
        string? Name,
        string? Description,
        string? Icon,
        JsonElement? Config,
        Guid? CompanyId);

    private sealed record DashboardListItem(
        Guid Id,
        string Name,
        string? Description,
        string Icon,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record DashboardDetails(
        Guid Id,
        Guid CompanyId,
        Guid OwnerUserId,
        string Name,
        string? Description,
        string Icon,
        JsonElement Config,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
