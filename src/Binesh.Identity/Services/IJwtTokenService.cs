using Binesh.Domain.Identity;

namespace Binesh.Identity.Services;

public interface IJwtTokenService
{
    /// <summary>Builds a signed JWT access token for the given user.</summary>
    string IssueAccessToken(User user, IEnumerable<string> roles);

    /// <summary>
    /// Builds a 60-second-lived JWT carrying the user's id with the
    /// <c>"binesh-ai-ws"</c> audience. Used by the WebSocket chat endpoint
    /// to authenticate connections without a Bearer header.
    /// </summary>
    ChatTicket IssueChatTicket(Guid userId);

    /// <summary>
    /// Validates a chat ticket and returns the carried user id. Throws if
    /// the ticket is expired, has the wrong audience, or fails signature
    /// validation.
    /// </summary>
    Guid ValidateChatTicket(string ticket);
}

public sealed record ChatTicket(string Token, DateTimeOffset ExpiresAt);
