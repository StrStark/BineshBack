using FluentValidation;

namespace Binesh.Application.Features.Chat.ListConversations;

public sealed class ListConversationsValidator : AbstractValidator<ListConversationsQuery>
{
    public ListConversationsValidator()
    {
        RuleFor(q => q.UserId).NotEqual(Guid.Empty);
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
