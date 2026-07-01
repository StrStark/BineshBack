using Binesh.Application.Abstractions;
using Binesh.Application.Features.Chat.Shared;
using Binesh.Domain.Chat;
using MediatR;

namespace Binesh.Application.Features.Chat.StartConversation;

public sealed class StartConversationHandler(IBineshDbContext db)
    : IRequestHandler<StartConversationCommand, ConversationDto>
{
    public async Task<ConversationDto> Handle(StartConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = Conversation.Start(request.UserId, request.Title);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);

        return new ConversationDto(
            conversation.Id, conversation.Title, conversation.CreatedAt, conversation.ArchivedAt, MessageCount: 0);
    }
}
