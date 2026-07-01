using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Binesh.Ai.Orchestration;
using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Domain.Chat;
using Binesh.Identity.Features.Auth.IssueChatTicket;
using Binesh.Identity.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Binesh.Api.Endpoints.Chat;

/// <summary>
/// Round 13b — WebSocket streaming for AI chat plus the ticket endpoint
/// that authenticates the WS connection. Persistence reuses Round 13a's
/// jsonb schema; tokens are streamed to the client as they arrive and the
/// DB write happens once at the end so disconnects mid-stream don't leave
/// orphan rows.
/// </summary>
public static class ChatStreamingEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatStreamingEndpoints(this IEndpointRouteBuilder routes)
    {
        // The ticket endpoint takes the normal Bearer auth — it's the bridge
        // from "I am authenticated" to "here is a short-lived token I can
        // present in the WS query string".
        routes.MapPost("/api/ai/chat/ticket", IssueTicket)
            .WithTags("AiChat")
            .RequireAuthorization()
            .RequireRateLimiting("ai")
            .WithName("IssueChatTicket")
            .Produces<IssueChatTicketResponse>(StatusCodes.Status200OK);

        // The WS endpoint deliberately skips RequireAuthorization — the
        // ticket query-string parameter IS the auth. Anonymous access here
        // is by design but every connection must come with a valid ticket.
        routes.MapGet("/api/ai/chat/ws", HandleWebSocket)
            .WithTags("AiChat")
            .AllowAnonymous()
            .WithName("AiChatWebSocket");

        return routes;
    }

    private static async Task<IResult> IssueTicket(
        ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."));
        var result = await mediator.Send(new IssueChatTicketCommand(userId), ct);
        return Results.Ok(result);
    }

    private static async Task HandleWebSocket(
        HttpContext context,
        IJwtTokenService jwt,
        IBineshDbContext db,
        AiOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Expected a WebSocket upgrade request.", cancellationToken);
            return;
        }

        var ticket = context.Request.Query["ticket"].ToString();
        if (string.IsNullOrEmpty(ticket))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Guid userId;
        try
        {
            userId = jwt.ValidateChatTicket(ticket);
        }
        catch (SecurityTokenException)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        try
        {
            await DriveOneExchangeAsync(ws, userId, db, orchestrator, cancellationToken);
        }
        finally
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cancellationToken);
            }
        }
    }

    private static async Task DriveOneExchangeAsync(
        WebSocket ws,
        Guid userId,
        IBineshDbContext db,
        AiOrchestrator orchestrator,
        CancellationToken ct)
    {
        // Frame 1 from client: {"conversationId": "...", "message": "..."}
        var inbound = await ReceiveTextAsync(ws, ct);
        if (inbound is null) { return; }

        ClientFrame request;
        try
        {
            request = JsonSerializer.Deserialize<ClientFrame>(inbound, JsonOpts)
                ?? throw new InvalidOperationException("Frame was null.");
        }
        catch (JsonException)
        {
            await SendAsync(ws, new ErrorFrame("bad_request", "Inbound frame is not valid JSON."), ct);
            return;
        }

        if (request.ConversationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Message))
        {
            await SendAsync(ws, new ErrorFrame("bad_request", "Both conversationId and message are required."), ct);
            return;
        }

        var conversation = await db.Conversations
            .AsNoTracking()
            .Where(c => c.Id == request.ConversationId && c.UserId == userId)
            .Select(c => new { c.Id, c.ArchivedAt })
            .SingleOrDefaultAsync(ct);

        if (conversation is null)
        {
            await SendAsync(ws, new ErrorFrame("not_found", "Conversation not found."), ct);
            return;
        }
        if (conversation.ArchivedAt is not null)
        {
            await SendAsync(ws, new ErrorFrame("conflict", "Conversation is archived."), ct);
            return;
        }

        var existing = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.Sequence)
            .Select(m => new { m.Sequence, m.Role, m.Content })
            .ToListAsync(ct);

        var history = existing
            .Where(m => m.Role is MessageRole.User or MessageRole.Assistant)
            .Select(m => new AiHistoryTurn(
                m.Role == MessageRole.User ? AiHistoryRole.User : AiHistoryRole.Assistant,
                ExtractText(m.Content)))
            .ToList();

        var assistantText = new StringBuilder();
        OrchestratorStreamFinal? final = null;

        try
        {
            await foreach (var ev in orchestrator.RunStreamingAsync(request.Message, userId, history, ct))
            {
                switch (ev)
                {
                    case OrchestratorStreamToken t:
                        assistantText.Append(t.Text);
                        await SendAsync(ws, new TokenFrame(t.Text), ct);
                        break;
                    case OrchestratorStreamToolCallDispatched td:
                        await SendAsync(ws, new ToolCallFrame("dispatched", td.ToolName, td.ArgumentsJson, null, null), ct);
                        break;
                    case OrchestratorStreamToolCallCompleted tc:
                        await SendAsync(ws, new ToolCallFrame("completed", tc.ToolName, null, tc.ResultJson, tc.Error), ct);
                        break;
                    case OrchestratorStreamFinal f:
                        final = f;
                        break;
                }
            }
        }
        catch (TooManyRequestsException ex)
        {
            await SendAsync(ws, new ErrorFrame("rate_exceeded", ex.Message), ct);
            return;
        }

        if (final is null) { return; }

        // Persist user + assistant rows in one SaveChanges; if we never reach
        // here (the WS was disconnected mid-stream), nothing is written.
        var nextSequence = existing.Count == 0 ? 1 : existing[^1].Sequence + 1;
        var userPayload = JsonSerializer.Serialize(new { text = request.Message }, JsonOpts);
        var assistantPayload = JsonSerializer.Serialize(new
        {
            text = final.AssistantText,
            finishReason = final.FinishReason,
            tokensUsed = final.TokensUsed,
            toolCalls = final.ToolCalls.Select(c => new
            {
                toolName = c.ToolName,
                argumentsJson = c.ArgumentsJson,
                resultJson = c.ResultJson,
                error = c.Error,
            }).ToArray(),
        }, JsonOpts);

        var userMsg = ChatMessage.Create(conversation.Id, nextSequence, MessageRole.User, userPayload);
        var assistantMsg = ChatMessage.Create(conversation.Id, nextSequence + 1, MessageRole.Assistant, assistantPayload);
        db.ChatMessages.Add(userMsg);
        db.ChatMessages.Add(assistantMsg);
        await db.SaveChangesAsync(ct);

        await SendAsync(ws, new FinalFrame(
            assistantMsg.Id,
            final.FinishReason,
            final.TokensUsed), ct);
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        while (true)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close) { return null; }
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (result.EndOfMessage) { break; }
        }
        return sb.ToString();
    }

    private static Task SendAsync<T>(WebSocket ws, T payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize<object>(payload!, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        return ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static string ExtractText(string contentJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
            {
                return t.GetString() ?? string.Empty;
            }
        }
        catch (JsonException) { }
        return contentJson;
    }

    // ── Frame shapes ────────────────────────────────────────────────────────
    private sealed record ClientFrame(Guid ConversationId, string Message);
    private sealed record TokenFrame(string Text) { public string Type => "token"; }
    private sealed record ToolCallFrame(string Phase, string Name, string? ArgumentsJson, string? ResultJson, string? Error)
    {
        public string Type => "tool_call";
    }
    private sealed record FinalFrame(Guid MessageId, string FinishReason, int TokensUsed) { public string Type => "final"; }
    private sealed record ErrorFrame(string Code, string Message) { public string Type => "error"; }
}
