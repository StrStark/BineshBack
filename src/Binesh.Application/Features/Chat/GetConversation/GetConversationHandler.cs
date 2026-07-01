using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Chat.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Chat.GetConversation;

public sealed class GetConversationHandler(IBineshDbContext db)
    : IRequestHandler<GetConversationQuery, ConversationWithMessagesDto>
{
    public async Task<ConversationWithMessagesDto> Handle(GetConversationQuery request, CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations
            .AsNoTracking()
            .Where(c => c.Id == request.Id && c.UserId == request.UserId)
            .Select(c => new
            {
                c.Id, c.Title, c.CreatedAt, c.ArchivedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            throw new NotFoundException("Conversation", request.Id);
        }

        var messages = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.Id)
            .OrderBy(m => m.Sequence)
            .Select(m => new ChatMessageDto(m.Id, m.Sequence, m.Role.ToString(), m.Content, m.CreatedAt))
            .ToListAsync(cancellationToken);

        return new ConversationWithMessagesDto(
            conversation.Id, conversation.Title, conversation.CreatedAt, conversation.ArchivedAt, messages);
    }
}
