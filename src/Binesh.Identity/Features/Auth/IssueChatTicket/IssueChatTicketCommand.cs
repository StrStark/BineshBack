using MediatR;

namespace Binesh.Identity.Features.Auth.IssueChatTicket;

/// <summary>
/// Returns a short-lived JWT the caller can present in the WebSocket
/// query string to authenticate the AI chat connection. The endpoint is
/// HTTP-only and requires the normal Bearer auth so the ticket is bound to
/// the same identity that requested it.
/// </summary>
public sealed record IssueChatTicketCommand(Guid UserId) : IRequest<IssueChatTicketResponse>;

public sealed record IssueChatTicketResponse(string Ticket, DateTimeOffset ExpiresAt);
