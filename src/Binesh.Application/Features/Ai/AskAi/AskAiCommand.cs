using MediatR;

namespace Binesh.Application.Features.Ai.AskAi;

/// <summary>
/// Single-shot AI query: a user message in, a final assistant message + the
/// audit log of tool calls out. Multi-turn chat history lives in Round 13.
/// </summary>
public sealed record AskAiCommand(string Message, Guid UserId) : IRequest<AskAiResponse>;

public sealed record AskAiResponse(
    string AssistantText,
    string FinishReason,
    int TokensUsed,
    IReadOnlyList<AskAiToolCall> ToolCalls);

public sealed record AskAiToolCall(
    string ToolName,
    string ArgumentsJson,
    string ResultJson,
    string? Error);
