using Binesh.Application.Abstractions;
using Binesh.Application.Features.Chat.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Chat.ListConversations;

public sealed class ListConversationsHandler(IBineshDbContext db)
    : IRequestHandler<ListConversationsQuery, ListConversationsResponse>
{
    public async Task<ListConversationsResponse> Handle(ListConversationsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Conversations
            .AsNoTracking()
            .Where(c => c.UserId == request.UserId);

        if (!request.IncludeArchived)
        {
            query = query.Where(c => c.ArchivedAt == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ConversationDto(
                c.Id, c.Title, c.CreatedAt, c.ArchivedAt,
                db.ChatMessages.Count(m => m.ConversationId == c.Id)))
            .ToListAsync(cancellationToken);

        return new ListConversationsResponse(items, totalCount, request.Page, request.PageSize);
    }
}
