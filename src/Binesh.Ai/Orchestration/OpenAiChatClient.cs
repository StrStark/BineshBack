using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using Binesh.Ai.Configuration;
using Binesh.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Binesh.Ai.Orchestration;

/// <summary>
/// Real OpenAI-backed <see cref="IAiChatClient"/>. The orchestrator sets the
/// current user id in <see cref="AiRequestContext"/>; this client then uses
/// per-user provider settings when configured, falling back to global
/// <see cref="OpenAiSettings"/>.
/// </summary>
public sealed class OpenAiChatClient(
    IOptions<OpenAiSettings> settings,
    IUserAiSettingsResolver userSettingsResolver,
    AiRequestContext requestContext,
    ILogger<OpenAiChatClient> logger) : IAiChatClient
{
    private readonly OpenAiSettings _settings = settings.Value;

    public async Task<AiCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken cancellationToken)
    {
        var runtime = await ResolveRuntimeSettingsAsync(cancellationToken);
        try
        {
            return await CompleteOnceAsync(runtime.Client, runtime.Model, messages, tools, cancellationToken);
        }
        catch (ClientResultException ex) when (IsRateLimit(ex)
            && runtime.UsesGlobalSettings
            && !string.IsNullOrWhiteSpace(_settings.FallbackModel))
        {
            logger.LogWarning(
                "Primary model '{Primary}' returned 429; retrying with fallback '{Fallback}'.",
                runtime.Model, _settings.FallbackModel);

            return await CompleteOnceAsync(runtime.Client, _settings.FallbackModel!, messages, tools, cancellationToken);
        }
    }

    private static async Task<AiCompletionResult> CompleteOnceAsync(
        OpenAIClient client,
        string model,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken ct)
    {
        var chat = client.GetChatClient(model);
        var options = new ChatCompletionOptions();
        foreach (var t in tools) { options.Tools.Add(t); }

        var completion = await chat.CompleteChatAsync(messages, options, ct);
        var c = completion.Value;

        var text = c.Content.Count > 0 ? c.Content[0].Text : null;
        var toolCalls = c.ToolCalls?.Select(tc =>
            new AiToolCallRequest(tc.Id, tc.FunctionName, tc.FunctionArguments.ToString()))
            .ToList() ?? [];

        var usage = c.Usage is null
            ? AiTokenUsage.Zero
            : new AiTokenUsage(c.Usage.InputTokenCount, c.Usage.OutputTokenCount);

        return new AiCompletionResult(text, toolCalls, usage, model);
    }

    private static bool IsRateLimit(ClientResultException ex) => ex.Status == 429;

    public async IAsyncEnumerable<AiStreamUpdate> CompleteStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var runtime = await ResolveRuntimeSettingsAsync(cancellationToken);
        var chat = runtime.Client.GetChatClient(runtime.Model);
        var options = new ChatCompletionOptions();
        foreach (var t in tools) { options.Tools.Add(t); }

        var partials = new Dictionary<int, ToolCallAccumulator>();
        var usage = AiTokenUsage.Zero;
        string finishReason = "stop";

        await foreach (var update in chat.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return new AiStreamToken(part.Text);
                }
            }

            foreach (var tu in update.ToolCallUpdates)
            {
                if (!partials.TryGetValue(tu.Index, out var acc))
                {
                    acc = new ToolCallAccumulator();
                    partials[tu.Index] = acc;
                }
                if (!string.IsNullOrEmpty(tu.ToolCallId)) { acc.Id = tu.ToolCallId; }
                if (!string.IsNullOrEmpty(tu.FunctionName)) { acc.FunctionName = tu.FunctionName; }
                var argsChunk = tu.FunctionArgumentsUpdate?.ToString();
                if (!string.IsNullOrEmpty(argsChunk)) { acc.Arguments.Append(argsChunk); }
            }

            if (update.FinishReason.HasValue)
            {
                finishReason = update.FinishReason.Value.ToString().ToLowerInvariant();
            }

            if (update.Usage is not null)
            {
                usage = new AiTokenUsage(update.Usage.InputTokenCount, update.Usage.OutputTokenCount);
            }
        }

        foreach (var (_, acc) in partials.OrderBy(p => p.Key))
        {
            yield return new AiStreamToolCall(acc.Id, acc.FunctionName, acc.Arguments.ToString());
        }

        yield return new AiStreamFinished(finishReason, usage, runtime.Model);
    }

    private async Task<RuntimeOpenAiSettings> ResolveRuntimeSettingsAsync(CancellationToken ct)
    {
        if (requestContext.UserId is Guid userId)
        {
            var userSettings = await userSettingsResolver.ResolveAsync(userId, ct);
            if (userSettings is not null)
            {
                var baseUrl = string.IsNullOrWhiteSpace(userSettings.BaseUrl)
                    ? _settings.BaseUrl
                    : userSettings.BaseUrl!;
                var model = string.IsNullOrWhiteSpace(userSettings.Model)
                    ? _settings.Model
                    : userSettings.Model!;
                return new RuntimeOpenAiSettings(
                    BuildClient(userSettings.ApiKey, baseUrl),
                    model,
                    UsesGlobalSettings: false);
            }
        }

        return new RuntimeOpenAiSettings(
            BuildClient(_settings.ApiKey, _settings.BaseUrl),
            _settings.Model,
            UsesGlobalSettings: true);
    }

    private OpenAIClient BuildClient(string apiKey, string baseUrl)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(baseUrl),
            NetworkTimeout = _settings.Timeout,
        };

        return new OpenAIClient(new ApiKeyCredential(apiKey), options);
    }

    private sealed class ToolCallAccumulator
    {
        public string Id = string.Empty;
        public string FunctionName = string.Empty;
        public StringBuilder Arguments { get; } = new();
    }

    private sealed record RuntimeOpenAiSettings(
        OpenAIClient Client,
        string Model,
        bool UsesGlobalSettings);
}
