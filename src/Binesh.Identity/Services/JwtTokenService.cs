using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Binesh.Application.Abstractions;
using Binesh.Domain.Identity;
using Binesh.Identity.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Binesh.Identity.Services;

internal sealed class JwtTokenService(
    IOptions<JwtSettings> jwtOptions,
    X509Certificate2 signingCertificate) : IJwtTokenService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;
    private readonly X509Certificate2 _cert = signingCertificate;
    private static readonly JwtSecurityTokenHandler _handler = new();

    public string IssueAccessToken(User user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.PhoneNumber ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (user.CompanyId is Guid companyId)
        {
            claims.Add(new Claim(TenantClaimTypes.CompanyId, companyId.ToString()));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(_jwt.AccessTokenLifetime),
            signingCredentials: new SigningCredentials(
                new X509SecurityKey(_cert),
                SecurityAlgorithms.RsaSha256));

        return _handler.WriteToken(token);
    }

    private const string ChatTicketAudience = "binesh-ai-ws";
    private static readonly TimeSpan ChatTicketLifetime = TimeSpan.FromSeconds(60);

    public ChatTicket IssueChatTicket(Guid userId)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(ChatTicketLifetime);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: ChatTicketAudience,
            claims:
            [
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new X509SecurityKey(_cert),
                SecurityAlgorithms.RsaSha256));

        return new ChatTicket(_handler.WriteToken(token), expires);
    }

    public Guid ValidateChatTicket(string ticket)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = ChatTicketAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new X509SecurityKey(_cert),
            ClockSkew = TimeSpan.FromSeconds(5),
            NameClaimType = ClaimTypes.NameIdentifier,
        };

        var principal = _handler.ValidateToken(ticket, parameters, out _);
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? throw new SecurityTokenException("Chat ticket missing NameIdentifier claim.");
        return Guid.Parse(userIdClaim.Value);
    }
}
