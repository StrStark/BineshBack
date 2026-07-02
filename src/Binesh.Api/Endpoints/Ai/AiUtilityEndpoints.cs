using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Binesh.Ai.Configuration;
using Binesh.Application.Abstractions;
using Binesh.Application.Features.Analytics.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Binesh.Api.Endpoints.Ai;

internal static partial class AiUtilityEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapAiUtilityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/models", ListModels)
            .WithName("ListAiModels");
        group.MapGet("/monitoring", Monitoring)
            .WithName("GetAiMonitoring");
        group.MapPost("/generate-widget", GenerateWidget)
            .WithName("GenerateAiWidget");

        return group;
    }

    private static async Task<IResult> ListModels(
        string? apiUrl,
        string? apiKey,
        ClaimsPrincipal user,
        IUserAiSettingsResolver settingsResolver,
        IOptions<OpenAiSettings> globalOptions,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var provider = await ResolveProviderAsync(
            user,
            settingsResolver,
            globalOptions,
            apiKey,
            model: null,
            baseUrl: apiUrl,
            ct);

        if (string.IsNullOrWhiteSpace(provider.BaseUrl))
        {
            return Results.BadRequest(new { success = false, error = "apiUrl is required" });
        }

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{provider.BaseUrl.TrimEnd('/')}/models");
        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        }

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            return Results.Json(
                new { success = false, error = $"Provider returned {(int)response.StatusCode}: {Clip(text, 200)}" },
                statusCode: (int)response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var models = ReadModels(doc.RootElement)
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .ToList();

        return Results.Ok(new { success = true, data = models });
    }

    private static async Task<IResult> Monitoring(
        int? limit,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var userId = RequireUserId(user);
        var take = Math.Clamp(limit ?? 10, 1, 100);
        var settings = await db.UserAiSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var conversations = await db.Conversations.AsNoTracking()
            .Where(c => c.UserId == userId && c.ArchivedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .Select(c => new { c.Id, c.Title, c.CreatedAt })
            .ToListAsync(ct);
        var conversationIds = await db.Conversations.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.Id)
            .ToListAsync(ct);
        var totalMessages = conversationIds.Count == 0
            ? 0
            : await db.ChatMessages.AsNoTracking()
                .CountAsync(m => conversationIds.Contains(m.ConversationId), ct);

        return Results.Ok(ApiEnvelope.Success(new
        {
            configured = true,
            traces = conversations.Select(c => new
            {
                id = c.Id,
                name = c.Title,
                timestamp = c.CreatedAt,
                sessionsCount = 1,
            }),
            stats = new
            {
                totalConversations = conversationIds.Count,
                totalSessions = conversationIds.Count,
                totalMessages,
                model = settings?.Model ?? "not configured",
                apiKeyConfigured = !string.IsNullOrWhiteSpace(settings?.ApiKeyEncrypted),
                apiUrl = settings?.BaseUrl ?? string.Empty,
            },
        }));
    }

    private static async Task<IResult> GenerateWidget(
        [FromBody] GenerateWidgetRequest body,
        ClaimsPrincipal user,
        IUserAiSettingsResolver settingsResolver,
        IOptions<OpenAiSettings> globalOptions,
        IHttpClientFactory httpClientFactory,
        IBiAnalyticsService analytics,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Prompt))
        {
            return Results.BadRequest(ApiEnvelope.Error("Prompt is required.", StatusCodes.Status400BadRequest));
        }

        var provider = await ResolveProviderAsync(
            user,
            settingsResolver,
            globalOptions,
            body.ApiKey,
            body.Model,
            body.ApiUrl ?? body.BaseUrl,
            ct);
        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            return Results.BadRequest(ApiEnvelope.Error("API key is not configured.", StatusCodes.Status400BadRequest));
        }

        var schema = analytics.GetSchema(analytics.DefaultSourceId);
        var systemPrompt = BuildWidgetSystemPrompt(schema);
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(90);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = provider.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = body.Prompt },
            },
            temperature = 0.2,
            max_tokens = 2000,
        }, options: JsonOpts);

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            return Results.Json(
                ApiEnvelope.Error($"Provider returned {(int)response.StatusCode}: {Clip(text, 300)}", StatusCodes.Status400BadRequest),
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            return Results.BadRequest(ApiEnvelope.Error("Provider returned an empty response.", StatusCodes.Status400BadRequest));
        }

        var json = JsonObjectRegex().Match(content).Value;
        if (string.IsNullOrWhiteSpace(json))
        {
            return Results.BadRequest(ApiEnvelope.Error("Provider response did not contain a JSON object.", StatusCodes.Status400BadRequest));
        }

        JsonElement widgetConfig;
        try
        {
            widgetConfig = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts).Clone();
            ValidateGeneratedWidget(widgetConfig, schema);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(ApiEnvelope.Error($"Invalid provider JSON: {ex.Message}", StatusCodes.Status400BadRequest));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ApiEnvelope.Error(ex.Message, StatusCodes.Status400BadRequest));
        }

        return Results.Ok(ApiEnvelope.Success(widgetConfig));
    }

    private static async Task<ProviderSettings> ResolveProviderAsync(
        ClaimsPrincipal user,
        IUserAiSettingsResolver settingsResolver,
        IOptions<OpenAiSettings> globalOptions,
        string? apiKey,
        string? model,
        string? baseUrl,
        CancellationToken ct)
    {
        var userSettings = await settingsResolver.ResolveAsync(RequireUserId(user), ct);
        var global = globalOptions.Value;
        return new ProviderSettings(
            FirstNonBlank(apiKey, userSettings?.ApiKey, global.ApiKey) ?? string.Empty,
            FirstNonBlank(model, userSettings?.Model, global.Model) ?? "gpt-4o-mini",
            FirstNonBlank(baseUrl, userSettings?.BaseUrl, global.BaseUrl) ?? "https://api.openai.com/v1");
    }

    private static string BuildWidgetSystemPrompt(BiSourceSchemaDto schema)
    {
        var datasets = schema.Datasets.Select(dataset =>
        {
            var fields = string.Join(
                "\n",
                dataset.Fields.Select(f => $"    - {f.Name} ({f.Label}, type: {f.Type}, aggregations: {string.Join(", ", f.AllowedAggregations)})"));
            return $"- Dataset \"{dataset.Id}\" ({dataset.Label}):\n{fields}";
        });
        return $$"""
            You convert a user's dashboard request into a single JSON widget config.

            Available BI datasets:
            {{string.Join("\n", datasets)}}

            Allowed chartType values: {{string.Join(", ", schema.ChartTypes.Select(c => c.Id))}}
            Allowed aggregation values: {{string.Join(", ", schema.Aggregations)}}

            Rules:
            1. Return only one JSON object. No markdown and no explanation.
            2. Use dataset and field names exactly as listed above.
            3. Use numeric fields for sum, avg, min, and max. Count can be used for any field.
            4. Prefer Sale for generic sales requests, Product for product movement, Customer for customers,
               and Financial for finance.

            JSON shape:
            {
              "title": "widget title",
              "chartType": "bar | line | pie | area | table | card | map",
              "table": "Sale | Product | Customer | Financial",
              "columns": [{ "field": "fieldName" }],
              "values": [{ "field": "fieldName", "aggregation": "sum | avg | count | min | max" }],
              "rows": [],
              "filters": []
            }
            """;
    }

    private static void ValidateGeneratedWidget(JsonElement config, BiSourceSchemaDto schema)
    {
        if (config.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Widget config must be a JSON object.");
        }
        var title = ReadString(config, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Widget title is required.");
        }
        var table = ReadString(config, "table");
        var dataset = schema.Datasets.SingleOrDefault(d => string.Equals(d.Id, table, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Invalid dataset: {table}");
        var chartType = ReadString(config, "chartType");
        if (schema.ChartTypes.All(c => !string.Equals(c.Id, chartType, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Invalid chartType: {chartType}");
        }

        var fields = dataset.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var field in ReadWidgetFields(config, "columns").Concat(ReadWidgetFields(config, "rows")))
        {
            if (!fields.ContainsKey(field))
            {
                throw new InvalidOperationException($"Invalid field '{field}' for dataset '{dataset.Id}'.");
            }
        }

        foreach (var value in ReadWidgetValues(config))
        {
            if (!fields.TryGetValue(value.Field, out var field))
            {
                throw new InvalidOperationException($"Invalid field '{value.Field}' for dataset '{dataset.Id}'.");
            }
            if (!field.AllowedAggregations.Contains(value.Aggregation, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid aggregation '{value.Aggregation}' for field '{value.Field}'.");
            }
        }
    }

    private static IEnumerable<string> ReadWidgetFields(JsonElement config, string property)
    {
        if (!config.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }
        foreach (var item in array.EnumerateArray())
        {
            var field = ReadString(item, "field");
            if (!string.IsNullOrWhiteSpace(field))
            {
                yield return field;
            }
        }
    }

    private static IEnumerable<(string Field, string Aggregation)> ReadWidgetValues(JsonElement config)
    {
        if (!config.TryGetProperty("values", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }
        foreach (var item in array.EnumerateArray())
        {
            var field = ReadString(item, "field");
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }
            yield return (field, ReadString(item, "aggregation") ?? "sum");
        }
    }

    private static IReadOnlyList<AiModelDto> ReadModels(JsonElement root)
    {
        var source = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)
            ? data
            : root;
        if (source.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var models = new List<AiModelDto>();
        foreach (var item in source.EnumerateArray())
        {
            var id = ReadString(item, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }
            models.Add(new AiModelDto(id, ReadString(item, "owned_by")));
        }
        return models;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string Clip(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."));

    [GeneratedRegex("\\{[\\s\\S]*\\}")]
    private static partial Regex JsonObjectRegex();

    private sealed record ProviderSettings(string ApiKey, string Model, string BaseUrl);

    private sealed record AiModelDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("owned_by")] string? OwnedBy);

    private sealed record GenerateWidgetRequest(
        string? Prompt,
        string? ApiKey,
        string? Model,
        string? ApiUrl,
        string? BaseUrl);
}
