using Binesh.Application.Features.Chat.Shared;
using MediatR;

namespace Binesh.Application.Features.Chat.ListConversations;

/// <summary>
/// Lists the authenticated user's conversations. Archived rows are excluded
/// by default — pass <see cref="IncludeArchived"/> to surface them.
/// </summary>
public sealed record ListConversationsQuery(
    Guid UserId,
    bool IncludeArchived,
    int Page,
    int PageSize)
    : IRequest<ListConversationsResponse>;

public sealed record ListConversationsResponse(
    IReadOnlyList<ConversationDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
