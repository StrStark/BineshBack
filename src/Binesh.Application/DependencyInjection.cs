using System.Reflection;
using Binesh.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Application;

public static class DependencyInjection
{
    /// <summary>This assembly. Other modules pass their own so Api can wire them all.</summary>
    public static Assembly Assembly => typeof(DependencyInjection).Assembly;

    /// <summary>
    /// Registers MediatR handlers + FluentValidation validators + cross-cutting
    /// behaviors. Pass additional assemblies (e.g. <c>Binesh.Identity</c>) when
    /// other layers contribute MediatR slices.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        params Assembly[] additionalAssemblies)
    {
        var assemblies = new[] { Assembly }.Concat(additionalAssemblies).Distinct().ToArray();

        services.AddMediatR(cfg =>
        {
            foreach (var asm in assemblies)
            {
                cfg.RegisterServicesFromAssembly(asm);
            }
            // Order matters: logging wraps validation wraps handler.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        foreach (var asm in assemblies)
        {
            services.AddValidatorsFromAssembly(asm);
        }

        return services;
    }
}
