using OpenAI.Chat;

namespace Binesh.Ai.Orchestration;

/// <summary>
/// Abstraction over the OpenAI chat-completion call. Lets tests script
/// responses without hitting the real API. The orchestrator owns the
/// message-list state and just hands the current list to the client on each
/// turn of the tool-call loop.
/// </summary>
public interface IAiChatClient
{
    Task<AiCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken cancellationToken);

    /// <summary>
    /// Streaming variant. Yields tokens as they arrive, tool calls once fully
    /// assembled, and finally one <see cref="AiStreamFinished"/> with usage.
    /// </summary>
    IAsyncEnumerable<AiStreamUpdate> CompleteStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken cancellationToken);
}

/// <summary>Output of one chat-completion turn.</summary>
public sealed record AiCompletionResult(
    string? AssistantText,
    IReadOnlyList<AiToolCallRequest> ToolCalls,
    AiTokenUsage Usage,
    string ModelUsed);

public sealed record AiToolCallRequest(string Id, string FunctionName, string ArgumentsJson);

/// <summary>
/// Incremental update from a streaming chat-completion call. The OpenAI
/// SDK delivers text in many small chunks and assembles tool calls across
/// several updates; this surface flattens that into discrete events the
/// orchestrator can fan out to the WS client.
/// </summary>
public abstract record AiStreamUpdate;

public sealed record AiStreamToken(string Text) : AiStreamUpdate;

/// <summary>Emitted once a tool call has been fully assembled from the SDK's incremental fragments.</summary>
public sealed record AiStreamToolCall(string Id, string FunctionName, string ArgumentsJson) : AiStreamUpdate;

/// <summary>Terminal event for one chat-completion turn. Carries usage + the finish reason.</summary>
public sealed record AiStreamFinished(string FinishReason, AiTokenUsage Usage, string ModelUsed) : AiStreamUpdate;

/// <summary>Token usage reported by the chat client. Zeros are treated as "unknown" by the budget enforcer.</summary>
public sealed record AiTokenUsage(int InputTokens, int OutputTokens)
{
    public int TotalTokens => InputTokens + OutputTokens;
    public static AiTokenUsage Zero => new(0, 0);
}
