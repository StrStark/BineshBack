using FluentValidation;
using MediatR;
using AppValidationException = Binesh.Application.Exceptions.ValidationException;

namespace Binesh.Application.Behaviors;

/// <summary>
/// Runs every registered FluentValidator for the incoming request before the handler.
/// On failure, throws our ValidationException which the API maps to 422 + per-field errors.
///
/// Replaces the per-action manual validation scattered across the old controllers.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new AppValidationException(failures);
        }

        return await next();
    }
}
