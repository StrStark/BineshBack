using System.Security.Claims;
using Binesh.Application.Features.Chat.ArchiveConversation;
using Binesh.Application.Features.Chat.GetConversation;
using Binesh.Application.Features.Chat.ListConversations;
using Binesh.Application.Features.Chat.SendChatMessage;
using Binesh.Application.Features.Chat.Shared;
using Binesh.Application.Features.Chat.StartConversation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Binesh.Api.Endpoints.Chat;

/// <summary>
/// Round 13a — multi-turn chat history endpoints. WebSocket streaming +
/// ticket auth lands in Round 13b on top of this; the data layer is shared.
/// </summary>
public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/ai/conversations")
            .WithTags("AiChat")
            .RequireAuthorization()
            .RequireRateLimiting("ai");

        group.MapPost("/", StartConversation)
            .WithName(nameof(StartConversation))
            .Produces<ConversationDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", ListConversations)
            .WithName(nameof(ListConversations))
            .Produces<ListConversationsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetConversation)
            .WithName(nameof(GetConversation))
            .Produces<ConversationWithMessagesDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", ArchiveConversation)
            .WithName(nameof(ArchiveConversation))
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/messages", SendChatMessage)
            .WithName(nameof(SendChatMessage))
            .Produces<SendChatMessageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        return routes;
    }

    private static async Task<IResult> StartConversation(
        [FromBody] StartConversationBody body,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken ct)
    {
        var userId = ResolveUserId(user);
        var result = await mediator.Send(new StartConversationCommand(userId, body.Title), ct);
        return Results.Created($"/api/ai/conversations/{result.Id}", result);
    }

    private static async Task<IResult> ListConversations(
        bool? includeArchived,
        int? page,
        int? pageSize,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken ct)
    {
        var userId = ResolveUserId(user);
        var result = await mediator.Send(
            new ListConversationsQuery(userId, includeArchived ?? false, page ?? 1, pageSize ?? 20), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetConversation(
        Guid id, ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var userId = ResolveUserId(user);
        var result = await mediator.Send(new GetConversationQuery(id, userId), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ArchiveConversation(
        Guid id, ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var userId = ResolveUserId(user);
        await mediator.Send(new ArchiveConversationCommand(id, userId), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SendChatMessage(
        Guid id,
        [FromBody] SendChatMessageBody body,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken ct)
    {
        var userId = ResolveUserId(user);
        var result = await mediator.Send(new SendChatMessageCommand(id, userId, body.Message), ct);
        return Results.Ok(result);
    }

    private static Guid ResolveUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."));

    public sealed record StartConversationBody(string Title);
    public sealed record SendChatMessageBody(string Message);
}
