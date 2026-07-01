using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using Binesh.Ai.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Binesh.Ai.Orchestration;

/// <summary>
/// Real OpenAI-backed <see cref="IAiChatClient"/>. Picks the model from
/// <see cref="OpenAiSettings"/>; on rate-limit (HTTP 429) retries once with
/// <see cref="OpenAiSettings.FallbackModel"/> if configured.
/// </summary>
public sealed class OpenAiChatClient(
    OpenAIClient client,
    IOptions<OpenAiSettings> settings,
    ILogger<OpenAiChatClient> logger) : IAiChatClient
{
    private readonly OpenAIClient _client = client;
    private readonly OpenAiSettings _settings = settings.Value;

    public async Task<AiCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken cancellationToken)
    {
        var primary = _settings.Model;
        try
        {
            return await CompleteOnceAsync(primary, messages, tools, cancellationToken);
        }
        catch (ClientResultException ex) when (IsRateLimit(ex) && !string.IsNullOrWhiteSpace(_settings.FallbackModel))
        {
            logger.LogWarning(
                "Primary model '{Primary}' returned 429; retrying with fallback '{Fallback}'.",
                primary, _settings.FallbackModel);

            return await CompleteOnceAsync(_settings.FallbackModel!, messages, tools, cancellationToken);
        }
    }

    private async Task<AiCompletionResult> CompleteOnceAsync(
        string model,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken ct)
    {
        var chat = _client.GetChatClient(model);
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
        // Streaming doesn't fall back to the fallback model — the WS protocol
        // doesn't expose a clean retry signal, and the budget enforcer will
        // already 429 before we get here when the user is over their cap.
        var chat = _client.GetChatClient(_settings.Model);
        var options = new ChatCompletionOptions();
        foreach (var t in tools) { options.Tools.Add(t); }

        // The SDK delivers tool-call arguments as many small fragments; we
        // accumulate by index until FinishReason arrives, then emit one
        // AiStreamToolCall per assembled call.
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

        yield return new AiStreamFinished(finishReason, usage, _settings.Model);
    }

    private sealed class ToolCallAccumulator
    {
        public string Id = string.Empty;
        public string FunctionName = string.Empty;
        public StringBuilder Arguments { get; } = new();
    }
}
