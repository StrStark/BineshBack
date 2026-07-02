using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Identity.Configuration;
using Binesh.Identity.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Binesh.Identity.Features.Auth.VerifyOtp;

/// <summary>
/// Verifies the OTP, then issues an access token + a fresh refresh token.
/// Creates a new <see cref="UserSession"/> per login (one per device).
///
/// Fixes from the old code:
///   - No magic <c>"696969"</c> backdoor.
///   - Refresh-token expiry is correctly set (old code used
///     <c>AddDays(TotalMinutes)</c>, making tokens valid for ~55 years).
///   - Lockout counter increments on every failed verify (the old code only
///     incremented in one of the two parallel signup/signin paths).
/// </summary>
public sealed class VerifyOtpHandler(
    UserManager<User> userManager,
    IBineshDbContext db,
    IOtpService otpService,
    IJwtTokenService jwtTokenService,
    IOptions<JwtSettings> jwtOptions,
    IOptions<SeedSettings> seedOptions)
    : IRequestHandler<VerifyOtpCommand, VerifyOtpResponse>
{
    public async Task<VerifyOtpResponse> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumberNormalizer.Normalize(request.PhoneNumber)!;

        var user = await userManager.Users
            .SingleOrDefaultAsync(u => u.PhoneNumber == phone, cancellationToken)
            ?? throw new UnauthorizedException("Invalid OTP.");  // don't reveal whether user exists

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new TooManyRequestsException("Account is temporarily locked. Try again later.");
        }

        var valid = await otpService.VerifyAsync(user, request.Otp);
        if (!valid)
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException("Invalid OTP.");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        // Phone is implicitly confirmed by a successful OTP.
        var userChanged = false;
        if (!user.PhoneNumberConfirmed)
        {
            user.PhoneNumberConfirmed = true;
            userChanged = true;
        }

        if (user.CompanyId is null)
        {
            user.CompanyId = await ResolveDefaultCompanyIdAsync(cancellationToken);
            userChanged = true;
        }

        if (userChanged)
        {
            await userManager.UpdateAsync(user);
        }

        // Start a new session for this login.
        var session = UserSession.Start(
            user.Id,
            request.DeviceInfo,
            request.IpAddress,
            request.UserAgent);

        db.Sessions.Add(session);

        // Issue the first refresh token in the session's chain.
        var jwt = jwtOptions.Value;
        var (rawRefresh, refreshEntity) = RefreshToken.Issue(session.Id, jwt.RefreshTokenLifetime);
        db.RefreshTokens.Add(refreshEntity);

        await db.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.IssueAccessToken(user, roles);

        return new VerifyOtpResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefresh,
            AccessTokenExpiresAt: DateTime.UtcNow.Add(jwt.AccessTokenLifetime),
            RefreshTokenExpiresAt: refreshEntity.ExpiresAt.UtcDateTime);
    }

    private async Task<Guid> ResolveDefaultCompanyIdAsync(CancellationToken cancellationToken)
    {
        var seed = seedOptions.Value.Company;
        var slug = string.IsNullOrWhiteSpace(seed.Slug) ? "binesh" : seed.Slug.Trim().ToLowerInvariant();

        var companyId = await db.Companies
            .Where(c => c.Slug == slug)
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(cancellationToken);

        companyId ??= await db.Companies
            .OrderBy(c => c.CreatedAt)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return companyId
            ?? throw new InvalidOperationException("No company exists to attach the authenticated user.");
    }
}
