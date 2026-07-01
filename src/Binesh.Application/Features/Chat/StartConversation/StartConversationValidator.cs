using FluentValidation;

namespace Binesh.Application.Features.Chat.StartConversation;

public sealed class StartConversationValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationValidator()
    {
        RuleFor(c => c.UserId).NotEqual(Guid.Empty);
        RuleFor(c => c.Title).NotEmpty().MaximumLength(256);
    }
}
