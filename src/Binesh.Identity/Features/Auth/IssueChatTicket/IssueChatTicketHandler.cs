using Binesh.Identity.Services;
using MediatR;

namespace Binesh.Identity.Features.Auth.IssueChatTicket;

public sealed class IssueChatTicketHandler(IJwtTokenService jwt) : IRequestHandler<IssueChatTicketCommand, IssueChatTicketResponse>
{
    public Task<IssueChatTicketResponse> Handle(IssueChatTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = jwt.IssueChatTicket(request.UserId);
        return Task.FromResult(new IssueChatTicketResponse(ticket.Token, ticket.ExpiresAt));
    }
}
