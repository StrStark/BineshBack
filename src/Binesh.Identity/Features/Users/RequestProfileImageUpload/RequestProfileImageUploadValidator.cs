using FluentValidation;

namespace Binesh.Identity.Features.Users.RequestProfileImageUpload;

public sealed class RequestProfileImageUploadValidator : AbstractValidator<RequestProfileImageUploadCommand>
{
    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg", "image/png", "image/webp",
    ];

    public RequestProfileImageUploadValidator()
    {
        RuleFor(c => c.UserId).NotEqual(Guid.Empty);
        RuleFor(c => c.ContentType)
            .NotEmpty()
            .Must(ct => AllowedContentTypes.Contains(ct.ToLowerInvariant()))
            .WithMessage($"ContentType must be one of: {string.Join(", ", AllowedContentTypes)}.");
    }
}
