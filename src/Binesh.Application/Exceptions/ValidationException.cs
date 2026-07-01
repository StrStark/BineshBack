using FluentValidation.Results;

namespace Binesh.Application.Exceptions;

/// <summary>
/// Thrown by the ValidationBehavior when FluentValidation rules fail.
/// The handler emits a 422 with per-field error details.
/// </summary>
public sealed class ValidationException : AppException
{
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation errors occurred.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray());
    }

    public override int StatusCode => 422;
    public override string ErrorCode => "validation.failed";

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
