using System.Text.Json;
using Binesh.Ai.Orchestration;
using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Chat.SendChatMessage;
using Binesh.Application.Features.Chat.Shared;
using Binesh.Domain.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Ai.Application;

/// <summary>
/// Persists one user→assistant exchange against a conversation. Loads the
/// prior history into the orchestrator (text-only — tool calls are not
/// replayed), stores the new user message + the new assistant message with
/// the full tool-call audit log embedded in the assistant message's
/// <c>Content</c> jsonb.
/// </summary>
public sealed class SendChatMessageHandler(IBineshDbContext db, AiOrchestrator orchestrator)
    : IRequestHandler<SendChatMessageCommand, SendChatMessageResponse>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<SendChatMessageResponse> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        // Load metadata only — we never mutate the conversation row itself, and
        // touching the entity through tracking + a navigation Include
        // confused EF's change tracker into emitting a phantom UPDATE that
        // reported 0 rows affected.
        var conversation = await db.Conversations
            .AsNoTracking()
            .Where(c => c.Id == request.ConversationId && c.UserId == request.UserId)
            .Select(c => new { c.Id, c.ArchivedAt })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Conversation", request.ConversationId);

        if (conversation.ArchivedAt is not null)
        {
            throw new ConflictException("Conversation is archived; unarchive it before sending new messages.");
        }

        var existing = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.Sequence)
            .Select(m => new { m.Sequence, m.Role, m.Content })
            .ToListAsync(cancellationToken);

        var history = existing
            .Where(m => m.Role is MessageRole.User or MessageRole.Assistant)
            .Select(m => new AiHistoryTurn(
                m.Role == MessageRole.User ? AiHistoryRole.User : AiHistoryRole.Assistant,
                ExtractText(m.Content)))
            .ToList();

        var run = await orchestrator.RunAsync(request.Message, request.UserId, history, cancellationToken);

        var userPayload = JsonSerializer.Serialize(new UserMessagePayload(request.Message), JsonOpts);
        var assistantPayload = JsonSerializer.Serialize(new AssistantMessagePayload(
            run.AssistantText,
            run.FinishReason,
            run.TokensUsed,
            run.ToolCalls.Select(c => new AssistantToolCallPayload(c.ToolName, c.ArgumentsJson, c.ResultJson, c.Error)).ToList()), JsonOpts);

        // Materialize a transient Conversation just so we can use its
        // AppendMessage factory — it has the sequence/Validation logic.
        var nextSequence = existing.Count == 0 ? 1 : existing[^1].Sequence + 1;
        var userMsg = ChatMessage.Create(conversation.Id, nextSequence, MessageRole.User, userPayload);
        var assistantMsg = ChatMessage.Create(conversation.Id, nextSequence + 1, MessageRole.Assistant, assistantPayload);

        db.ChatMessages.Add(userMsg);
        db.ChatMessages.Add(assistantMsg);
        await db.SaveChangesAsync(cancellationToken);

        return new SendChatMessageResponse(
            new ChatMessageDto(userMsg.Id, userMsg.Sequence, userMsg.Role.ToString(), userMsg.Content, userMsg.CreatedAt),
            new ChatMessageDto(assistantMsg.Id, assistantMsg.Sequence, assistantMsg.Role.ToString(), assistantMsg.Content, assistantMsg.CreatedAt),
            run.FinishReason,
            run.TokensUsed);
    }

    /// <summary>
    /// Pulls the plain text out of a message's content JSON. Both user and
    /// assistant payloads carry a top-level <c>text</c> field; if it's
    /// missing or unparseable the original content is replayed verbatim so
    /// nothing is silently dropped from history.
    /// </summary>
    private static string ExtractText(string contentJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            {
                return textProp.GetString() ?? string.Empty;
            }
        }
        catch (JsonException) { /* fall through */ }
        return contentJson;
    }

    private sealed record UserMessagePayload(string Text);

    private sealed record AssistantMessagePayload(
        string Text,
        string FinishReason,
        int TokensUsed,
        IReadOnlyList<AssistantToolCallPayload> ToolCalls);

    private sealed record AssistantToolCallPayload(string ToolName, string ArgumentsJson, string ResultJson, string? Error);
}
