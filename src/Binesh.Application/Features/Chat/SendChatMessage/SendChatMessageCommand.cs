using Binesh.Application.Features.Chat.Shared;
using MediatR;

namespace Binesh.Application.Features.Chat.SendChatMessage;

/// <summary>
/// Posts one user turn to a conversation, drives the AI orchestrator with the
/// prior history, and persists both the new user message and the new
/// assistant message. Tool-call audit log lives inside the assistant
/// message's <c>Content</c> jsonb so the UI can render a collapsible
/// "show details" panel.
/// </summary>
public sealed record SendChatMessageCommand(Guid ConversationId, Guid UserId, string Message)
    : IRequest<SendChatMessageResponse>;

public sealed record SendChatMessageResponse(
    ChatMessageDto UserMessage,
    ChatMessageDto AssistantMessage,
    string FinishReason,
    int TokensUsed);
