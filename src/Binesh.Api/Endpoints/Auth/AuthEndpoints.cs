using Binesh.Identity.Features.Auth.Refresh;
using Binesh.Identity.Features.Auth.RequestOtp;
using Binesh.Identity.Features.Auth.SignOut;
using Binesh.Identity.Features.Auth.VerifyOtp;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Binesh.Api.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        // Strict rate limit on the SMS-sending endpoint — protects the budget.
        group.MapPost("/otp/request", RequestOtp)
            .WithName(nameof(RequestOtp))
            .WithSummary("Send an OTP via SMS to the given phone number.")
            .RequireRateLimiting("auth")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapPost("/otp/verify", VerifyOtp)
            .WithName(nameof(VerifyOtp))
            .WithSummary("Verify the OTP and issue a token pair.")
            .RequireRateLimiting("auth")
            .Produces<VerifyOtpResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        group.MapPost("/refresh", Refresh)
            .WithName(nameof(Refresh))
            .WithSummary("Rotate a refresh token into a new access + refresh pair.")
            .Produces<RefreshResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        group.MapPost("/signout", SignOut)
            .WithName(nameof(SignOut))
            .WithSummary("Revoke the session backing the given refresh token.")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return routes;
    }

    // ── Handlers ────────────────────────────────────────────────────────────

    private static async Task<IResult> RequestOtp(
        [FromBody] RequestOtpRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new RequestOtpCommand(body.PhoneNumber), ct);
        return Results.Ok();   // always 200 — no user enumeration
    }

    private static async Task<IResult> VerifyOtp(
        [FromBody] VerifyOtpRequest body,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var ua = httpContext.Request.Headers.UserAgent.ToString();

        var result = await mediator.Send(
            new VerifyOtpCommand(body.PhoneNumber, body.Otp, body.DeviceInfo, ua, ip), ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> Refresh(
        [FromBody] RefreshRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshCommand(body.RefreshToken), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SignOut(
        [FromBody] SignOutRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new SignOutCommand(body.RefreshToken), ct);
        return Results.Ok();
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    public sealed record RequestOtpRequest(string PhoneNumber);
    public sealed record VerifyOtpRequest(string PhoneNumber, string Otp, string? DeviceInfo);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record SignOutRequest(string RefreshToken);
}
