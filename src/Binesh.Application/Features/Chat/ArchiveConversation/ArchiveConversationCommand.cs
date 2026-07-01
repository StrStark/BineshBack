using MediatR;

namespace Binesh.Application.Features.Chat.ArchiveConversation;

public sealed record ArchiveConversationCommand(Guid Id, Guid UserId) : IRequest;
