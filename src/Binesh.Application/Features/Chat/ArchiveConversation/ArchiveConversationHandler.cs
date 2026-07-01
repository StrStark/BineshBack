using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Chat.ArchiveConversation;

public sealed class ArchiveConversationHandler(IBineshDbContext db) : IRequestHandler<ArchiveConversationCommand>
{
    public async Task Handle(ArchiveConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations
            .SingleOrDefaultAsync(c => c.Id == request.Id && c.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Conversation", request.Id);

        conversation.Archive();
        await db.SaveChangesAsync(cancellationToken);
    }
}
