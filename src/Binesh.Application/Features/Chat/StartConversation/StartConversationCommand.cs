using Binesh.Application.Features.Chat.Shared;
using MediatR;

namespace Binesh.Application.Features.Chat.StartConversation;

public sealed record StartConversationCommand(Guid UserId, string Title) : IRequest<ConversationDto>;
