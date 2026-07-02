using System.Security.Claims;
using Binesh.Application.Features.Ai.AskAi;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Binesh.Api.Endpoints.Ai;

/// <summary>
/// Round 12c — single-shot AI query endpoint. Multi-turn chat (history,
/// WebSocket streaming, ticket auth) lands in Round 13.
/// </summary>
public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/ai")
            .WithTags("Ai")
            .RequireAuthorization()
            .RequireRateLimiting("ai");

        group.MapPost("/query", AskAi)
            .WithName(nameof(AskAi))
            .Produces<AskAiResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();
        group.MapAiUtilityEndpoints();

        return routes;
    }

    private static async Task<IResult> AskAi(
        [FromBody] AskAiBody body,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."));
        var result = await mediator.Send(new AskAiCommand(body.Message, userId), cancellationToken);
        return Results.Ok(result);
    }

    public sealed record AskAiBody(string Message);
}
