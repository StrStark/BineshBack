using Binesh.Application.Features.Chat.Shared;
using MediatR;

namespace Binesh.Application.Features.Chat.GetConversation;

/// <summary>
/// Returns one conversation plus its messages in sequence order. Throws
/// NotFound when the conversation belongs to a different user — the
/// existence of the id is itself privileged information.
/// </summary>
public sealed record GetConversationQuery(Guid Id, Guid UserId) : IRequest<ConversationWithMessagesDto>;
