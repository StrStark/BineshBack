using System.ClientModel;
using Binesh.Ai.Configuration;
using Binesh.Ai.Orchestration;
using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Ai.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Binesh.Ai;

public static class DependencyInjection
{
    /// <summary>Exposed so the Api composition root can hand this assembly to MediatR for handler scanning.</summary>
    public static System.Reflection.Assembly Assembly => typeof(DependencyInjection).Assembly;

    public static IServiceCollection AddBineshAi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OpenAiSettings>()
            .Bind(configuration.GetSection(OpenAiSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<OpenAiSettings>>().Value;

            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(settings.BaseUrl),
                NetworkTimeout = settings.Timeout,
            };

            return new OpenAIClient(new ApiKeyCredential(settings.ApiKey), options);
        });

        // Round 12a — schema registry foundation. CompiledSelectorCache is a
        // process-lifetime cache; SchemaRegistry holds the six built-in
        // schemas. Query engine + tools come in later sub-rounds.
        services.AddSingleton<CompiledSelectorCache>();
        services.AddSingleton<AiQueryEngine>();
        services.AddSingleton<SchemaRegistry>(_ =>
        {
            var registry = new SchemaRegistry();
            registry.Register(CustomerSchema.Build());
            registry.Register(ProductSchema.Build());
            registry.Register(InventoryEventSchema.Build());
            registry.Register(FinancialSchema.Build());
            registry.Register(SaleSchema.Build());
            registry.Register(SalesReturnSchema.Build());
            return registry;
        });

        // Round 12c — tool layer + orchestrator. Each concrete tool is
        // scoped to match IBineshDbContext (scoped). The registry pulls all
        // tools at request time so the LLM sees them per-request.
        services.AddScoped<IQueryableTool, CustomerQueryTool>();
        services.AddScoped<IQueryableTool, ProductQueryTool>();
        services.AddScoped<IQueryableTool, InventoryEventQueryTool>();
        services.AddScoped<IQueryableTool, FinancialQueryTool>();
        services.AddScoped<IQueryableTool, SaleQueryTool>();
        services.AddScoped<IQueryableTool, SalesReturnQueryTool>();
        services.AddScoped<IQueryableTool, DashboardAnalyticsTool>();

        services.AddScoped<QueryToolRegistry>(sp =>
        {
            var registry = new QueryToolRegistry();
            foreach (var tool in sp.GetServices<IQueryableTool>())
            {
                registry.Register(tool);
            }
            return registry;
        });

        services.AddScoped<AiRequestContext>();
        services.AddScoped<IAiChatClient, OpenAiChatClient>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenBudget, InMemoryTokenBudget>();
        services.AddScoped<AiOrchestrator>();

        return services;
    }
}
