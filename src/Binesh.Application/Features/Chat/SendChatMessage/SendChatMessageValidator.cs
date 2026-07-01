using FluentValidation;

namespace Binesh.Application.Features.Chat.SendChatMessage;

public sealed class SendChatMessageValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageValidator()
    {
        RuleFor(c => c.ConversationId).NotEqual(Guid.Empty);
        RuleFor(c => c.UserId).NotEqual(Guid.Empty);
        RuleFor(c => c.Message).NotEmpty().MaximumLength(8000);
    }
}
