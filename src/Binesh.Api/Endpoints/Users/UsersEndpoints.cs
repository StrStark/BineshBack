using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Binesh.Application.Abstractions;
using Binesh.Domain.Identity;
using Binesh.Identity.Features.Users.CreateUser;
using Binesh.Identity.Features.Users.DeleteUser;
using Binesh.Identity.Features.Users.GetMyProfile;
using Binesh.Identity.Features.Users.GetUserById;
using Binesh.Identity.Features.Users.ListUsers;
using Binesh.Identity.Features.Users.RequestProfileImageUpload;
using Binesh.Identity.Features.Users.SetProfileImage;
using Binesh.Identity.Features.Users.Shared;
using Binesh.Identity.Features.Users.UpdateMyProfile;
using Binesh.Identity.Features.Users.UpdateUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Api.Endpoints.Users;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users").WithTags("Users");

        // ── Self ────────────────────────────────────────────────────────────
        group.MapGet("/me", GetMyProfile)
            .RequireAuthorization()
            .WithName(nameof(GetMyProfile))
            .Produces<UserDto>(StatusCodes.Status200OK);

        group.MapPut("/me", UpdateMyProfile)
            .RequireAuthorization()
            .WithName(nameof(UpdateMyProfile))
            .Produces<UserDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/me/preferences", GetMyPreferences)
            .RequireAuthorization()
            .WithName(nameof(GetMyPreferences))
            .Produces(StatusCodes.Status200OK);

        group.MapPut("/me/preferences", UpsertMyPreferences)
            .RequireAuthorization()
            .WithName(nameof(UpsertMyPreferences))
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        // ── Self / profile image (Round 15) ─────────────────────────────────
        group.MapPost("/me/profile-image/upload-url", RequestProfileImageUploadUrl)
            .RequireAuthorization()
            .WithName(nameof(RequestProfileImageUploadUrl))
            .Produces<PresignedUploadUrl>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapPut("/me/profile-image", SetProfileImage)
            .RequireAuthorization()
            .WithName(nameof(SetProfileImage))
            .Produces<UserDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/me/profile-image", ClearProfileImage)
            .RequireAuthorization()
            .WithName(nameof(ClearProfileImage))
            .Produces<UserDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // ── Admin reads (SuperAdmin or Admin) ───────────────────────────────
        group.MapGet("/", ListUsers)
            .RequireAuthorization("role:Admin")
            .WithName(nameof(ListUsers))
            .Produces<ListUsersResponse>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetUserById)
            .RequireAuthorization("role:Admin")
            .WithName(nameof(GetUserById))
            .Produces<UserDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // ── SuperAdmin-only mutations ───────────────────────────────────────
        group.MapPost("/", CreateUser)
            .RequireAuthorization("role:SuperAdmin")
            .WithName(nameof(CreateUser))
            .Produces<UserDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateUser)
            .RequireAuthorization("role:SuperAdmin")
            .WithName(nameof(UpdateUser))
            .Produces<UserDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteUser)
            .RequireAuthorization("role:SuperAdmin")
            .WithName(nameof(DeleteUser))
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return routes;
    }

    // ── Handlers ────────────────────────────────────────────────────────────

    private static async Task<IResult> GetMyProfile(
        ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var id = RequireUserId(user);
        var result = await mediator.Send(new GetMyProfileQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> RequestProfileImageUploadUrl(
        [FromBody] RequestProfileImageUploadBody body,
        ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var id = RequireUserId(user);
        var result = await mediator.Send(
            new RequestProfileImageUploadCommand(id, body.ContentType), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SetProfileImage(
        [FromBody] SetProfileImageBody body,
        ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var id = RequireUserId(user);
        var result = await mediator.Send(new SetProfileImageCommand(id, body.ObjectKey), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ClearProfileImage(
        ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var id = RequireUserId(user);
        var result = await mediator.Send(new SetProfileImageCommand(id, ObjectKey: null), ct);
        return Results.Ok(result);
    }

    public sealed record RequestProfileImageUploadBody(string ContentType);
    public sealed record SetProfileImageBody(string ObjectKey);

    private static async Task<IResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileRequest body,
        ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var id = RequireUserId(user);
        var result = await mediator.Send(
            new UpdateMyProfileCommand(id, body.FirstName, body.LastName, body.JobTitle, body.BirthDate),
            ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetMyPreferences(
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var id = RequireUserId(user);
        var json = await db.UserPreferences
            .AsNoTracking()
            .Where(p => p.UserId == id)
            .Select(p => p.PreferencesJson)
            .SingleOrDefaultAsync(ct) ?? "{}";

        return Results.Ok(ApiEnvelope.Success(ParseObject(json)));
    }

    private static async Task<IResult> UpsertMyPreferences(
        [FromBody] JsonElement body,
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var id = RequireUserId(user);
        var preferences = await db.UserPreferences
            .SingleOrDefaultAsync(p => p.UserId == id, ct);

        if (preferences is null)
        {
            preferences = new UserPreferences { UserId = id };
            db.UserPreferences.Add(preferences);
        }

        var merged = ParseObject(preferences.PreferencesJson);
        var incoming = body.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(body.GetRawText()) as JsonObject
            : null;

        if (incoming is not null)
        {
            foreach (var item in incoming.ToList())
            {
                merged[item.Key] = item.Value?.DeepClone();
            }
        }

        preferences.PreferencesJson = merged.ToJsonString();
        preferences.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ApiEnvelope.Success(merged, "Preferences saved"));
    }

    private static async Task<IResult> ListUsers(
        string? search,
        int? page,
        int? pageSize,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListUsersQuery(search, page ?? 1, pageSize ?? 20), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetUserById(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetUserByIdQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateUser(
        [FromBody] CreateUserRequest body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateUserCommand(body.PhoneNumber, body.FirstName, body.LastName, body.JobTitle), ct);
        return Results.Created($"/api/users/{result.Id}", result);
    }

    private static async Task<IResult> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateUserCommand(id, body.FirstName, body.LastName, body.JobTitle, body.BirthDate), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteUser(
        Guid id, ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var requesterId = RequireUserId(user);
        await mediator.Send(new DeleteUserCommand(id, requesterId), ct);
        return Results.NoContent();
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."));

    private static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    public sealed record UpdateMyProfileRequest(string? FirstName, string? LastName, string? JobTitle, DateTimeOffset? BirthDate);
    public sealed record CreateUserRequest(string PhoneNumber, string? FirstName, string? LastName, string? JobTitle);
    public sealed record UpdateUserRequest(string? FirstName, string? LastName, string? JobTitle, DateTimeOffset? BirthDate);
}
