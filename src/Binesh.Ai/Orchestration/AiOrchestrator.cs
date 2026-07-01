using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Binesh.Ai.Prompts;
using Binesh.Ai.QueryEngine;
using Binesh.Ai.Tools;
using Binesh.Application.Exceptions;
using OpenAI.Chat;

namespace Binesh.Ai.Orchestration;

/// <summary>
/// Single-shot orchestrator. Takes a user message, runs the OpenAI
/// chat-completion loop with the registered query tools, dispatches every
/// tool call to the matching <see cref="IQueryableTool"/>, and returns the
/// final assistant text alongside an audit log of every tool call that ran.
///
/// <para>Multi-turn chat (history persistence, WebSocket streaming) lands
/// in Round 13. This component is intentionally stateless: callers own the
/// history; we just complete one user turn.</para>
/// </summary>
public sealed class AiOrchestrator(
    IAiChatClient chatClient,
    QueryToolRegistry toolRegistry,
    ITokenBudget tokenBudget)
{
    public const int MaxToolIterations = 5;

    public Task<AiOrchestratorResult> RunAsync(string userMessage, Guid userId, CancellationToken cancellationToken)
        => RunAsync(userMessage, userId, [], cancellationToken);

    public async Task<AiOrchestratorResult> RunAsync(
        string userMessage,
        Guid userId,
        IReadOnlyList<AiHistoryTurn> history,
        CancellationToken cancellationToken)
    {
        var systemPrompt = Prompts.Prompts.SystemInstructions + "\n\n" + QueryPromptBuilder.Build(toolRegistry);

        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };
        foreach (var turn in history)
        {
            messages.Add(turn.Role switch
            {
                AiHistoryRole.User => new UserChatMessage(turn.Text),
                AiHistoryRole.Assistant => new AssistantChatMessage(turn.Text),
                _ => throw new InvalidOperationException($"Unsupported history role '{turn.Role}'."),
            });
        }
        messages.Add(new UserChatMessage(userMessage));

        var tools = QueryToolBuilder.BuildAll(toolRegistry).ToList();
        var auditLog = new List<AiOrchestratorToolCall>();
        var totalTokens = 0;

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            if (!tokenBudget.CanProceed(userId))
            {
                throw new TooManyRequestsException(
                    "Daily AI token budget exhausted. Try again later.");
            }

            var result = await chatClient.CompleteAsync(messages, tools, cancellationToken);
            tokenBudget.Charge(userId, result.Usage.TotalTokens);
            totalTokens += result.Usage.TotalTokens;

            if (result.ToolCalls.Count == 0)
            {
                return new AiOrchestratorResult(
                    result.AssistantText ?? string.Empty, auditLog, FinishReason: "stop", TokensUsed: totalTokens);
            }

            // Append the assistant message that requested the tools.
            messages.Add(new AssistantChatMessage(
                result.ToolCalls.Select(tc =>
                    ChatToolCall.CreateFunctionToolCall(tc.Id, tc.FunctionName, BinaryData.FromString(tc.ArgumentsJson)))));

            foreach (var call in result.ToolCalls)
            {
                var (resultText, error) = await DispatchToolCallAsync(call, cancellationToken);
                auditLog.Add(new AiOrchestratorToolCall(call.FunctionName, call.ArgumentsJson, resultText, error));
                messages.Add(new ToolChatMessage(call.Id, resultText));
            }
        }

        // Ran out of iterations — return whatever assistant text we have plus the audit log.
        return new AiOrchestratorResult(
            AssistantText: "I'm sorry, I exhausted my tool budget before reaching a final answer. Please try a more specific question.",
            ToolCalls: auditLog,
            FinishReason: "max_iterations",
            TokensUsed: totalTokens);
    }

    /// <summary>
    /// Streaming version of <see cref="RunAsync(string, Guid, IReadOnlyList{AiHistoryTurn}, CancellationToken)"/>.
    /// Yields tokens as the model produces them, fans out tool-call lifecycle
    /// events as the inner loop runs, and ends with one
    /// <see cref="OrchestratorStreamFinal"/> carrying the assembled assistant
    /// text + finish reason + token usage + audit log. Persistence is the
    /// caller's responsibility — they accumulate tokens and only write to
    /// the DB after the terminal event so disconnects don't leave orphans.
    /// </summary>
    public async IAsyncEnumerable<OrchestratorStreamEvent> RunStreamingAsync(
        string userMessage,
        Guid userId,
        IReadOnlyList<AiHistoryTurn> history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var systemPrompt = Prompts.Prompts.SystemInstructions + "\n\n" + QueryPromptBuilder.Build(toolRegistry);

        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };
        foreach (var turn in history)
        {
            messages.Add(turn.Role switch
            {
                AiHistoryRole.User => new UserChatMessage(turn.Text),
                AiHistoryRole.Assistant => new AssistantChatMessage(turn.Text),
                _ => throw new InvalidOperationException($"Unsupported history role '{turn.Role}'."),
            });
        }
        messages.Add(new UserChatMessage(userMessage));

        var tools = QueryToolBuilder.BuildAll(toolRegistry).ToList();
        var auditLog = new List<AiOrchestratorToolCall>();
        var assembledText = new StringBuilder();
        var totalTokens = 0;
        string finishReason = "stop";

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            if (!tokenBudget.CanProceed(userId))
            {
                throw new TooManyRequestsException(
                    "Daily AI token budget exhausted. Try again later.");
            }

            var turnText = new StringBuilder();
            var turnToolCalls = new List<AiToolCallRequest>();
            AiTokenUsage turnUsage = AiTokenUsage.Zero;
            string turnFinishReason = "stop";

            await foreach (var update in chatClient.CompleteStreamingAsync(messages, tools, cancellationToken))
            {
                switch (update)
                {
                    case AiStreamToken token:
                        turnText.Append(token.Text);
                        assembledText.Append(token.Text);
                        yield return new OrchestratorStreamToken(token.Text);
                        break;
                    case AiStreamToolCall call:
                        turnToolCalls.Add(new AiToolCallRequest(call.Id, call.FunctionName, call.ArgumentsJson));
                        break;
                    case AiStreamFinished finished:
                        turnUsage = finished.Usage;
                        turnFinishReason = finished.FinishReason;
                        break;
                }
            }

            tokenBudget.Charge(userId, turnUsage.TotalTokens);
            totalTokens += turnUsage.TotalTokens;
            finishReason = turnFinishReason;

            if (turnToolCalls.Count == 0)
            {
                yield return new OrchestratorStreamFinal(assembledText.ToString(), finishReason, totalTokens, auditLog);
                yield break;
            }

            // The assistant message that requested the tools is reconstructed for the next turn.
            messages.Add(new AssistantChatMessage(
                turnToolCalls.Select(tc =>
                    ChatToolCall.CreateFunctionToolCall(tc.Id, tc.FunctionName, BinaryData.FromString(tc.ArgumentsJson)))));

            foreach (var call in turnToolCalls)
            {
                yield return new OrchestratorStreamToolCallDispatched(call.FunctionName, call.ArgumentsJson);
                var (resultText, error) = await DispatchToolCallAsync(call, cancellationToken);
                auditLog.Add(new AiOrchestratorToolCall(call.FunctionName, call.ArgumentsJson, resultText, error));
                yield return new OrchestratorStreamToolCallCompleted(call.FunctionName, resultText, error);
                messages.Add(new ToolChatMessage(call.Id, resultText));
            }
        }

        yield return new OrchestratorStreamFinal(
            "I'm sorry, I exhausted my tool budget before reaching a final answer. Please try a more specific question.",
            "max_iterations", totalTokens, auditLog);
    }

    private async Task<(string ResultJson, string? Error)> DispatchToolCallAsync(
        AiToolCallRequest call, CancellationToken cancellationToken)
    {
        var tool = toolRegistry.Get(call.FunctionName);
        if (tool is null)
        {
            var err = $"Unknown tool '{call.FunctionName}'.";
            return (JsonSerializer.Serialize(new { error = err }), err);
        }

        AiQueryRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AiQueryRequest>(
                call.ArgumentsJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException ex)
        {
            var err = $"Tool arguments could not be parsed as AiQueryRequest: {ex.Message}";
            return (JsonSerializer.Serialize(new { error = err }), err);
        }

        if (request is null)
        {
            var err = "Tool arguments deserialized to null.";
            return (JsonSerializer.Serialize(new { error = err }), err);
        }

        try
        {
            var data = await tool.ExecuteAsync(request, cancellationToken);
            return (JsonSerializer.Serialize(data), null);
        }
        catch (InvalidOperationException ex)
        {
            // Surface validator / engine errors back to the model so it can self-correct.
            var err = ex.Message;
            return (
                JsonSerializer.Serialize(new
                {
                    error = err,
                    hint = "Use only fields defined for this entity. Aggregate aliases cannot appear in OrderBy/Filters/GroupBy.",
                }),
                err);
        }
    }
}

public sealed record AiOrchestratorResult(
    string AssistantText,
    IReadOnlyList<AiOrchestratorToolCall> ToolCalls,
    string FinishReason,
    int TokensUsed);

/// <summary>
/// Replayable history turn. Only user/assistant text is preserved across
/// turns — tool calls are NOT replayed (the LLM gets a fresh tool-call loop
/// per turn, just like chat UIs that show the assistant's summary).
/// </summary>
public sealed record AiHistoryTurn(AiHistoryRole Role, string Text);

public enum AiHistoryRole
{
    User = 1,
    Assistant = 2,
}

/// <summary>
/// Event emitted by <see cref="AiOrchestrator.RunStreamingAsync"/>. Tokens
/// flow during model output; tool-call lifecycle is reported as the loop
/// runs; one terminal <see cref="OrchestratorStreamFinal"/> ends the stream.
/// </summary>
public abstract record OrchestratorStreamEvent;

public sealed record OrchestratorStreamToken(string Text) : OrchestratorStreamEvent;

public sealed record OrchestratorStreamToolCallDispatched(string ToolName, string ArgumentsJson) : OrchestratorStreamEvent;

public sealed record OrchestratorStreamToolCallCompleted(string ToolName, string ResultJson, string? Error) : OrchestratorStreamEvent;

public sealed record OrchestratorStreamFinal(
    string AssistantText,
    string FinishReason,
    int TokensUsed,
    IReadOnlyList<AiOrchestratorToolCall> ToolCalls) : OrchestratorStreamEvent;

public sealed record AiOrchestratorToolCall(
    string ToolName,
    string ArgumentsJson,
    string ResultJson,
    string? Error);
