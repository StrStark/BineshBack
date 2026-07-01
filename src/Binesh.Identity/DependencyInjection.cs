using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Bootstrap;
using Binesh.Identity.Configuration;
using Binesh.Identity.Services;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Binesh.Identity;

public static class DependencyInjection
{
    /// <summary>Exposed so Api / tests can scan this assembly for MediatR handlers and validators.</summary>
    public static Assembly Assembly => typeof(DependencyInjection).Assembly;

    public static IServiceCollection AddBineshIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Settings ─────────────────────────────────────────────────────────
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<IdentitySettings>()
            .Bind(configuration.GetSection(IdentitySettings.SectionName))
            .ValidateOnStart();

        services.AddOptions<SmsSettings>()
            .Bind(configuration.GetSection(SmsSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SeedSettings>()
            .Bind(configuration.GetSection(SeedSettings.SectionName))
            .ValidateOnStart();

        // ── ASP.NET Identity ─────────────────────────────────────────────────
        services.AddIdentity<User, Role>(options =>
        {
            var identity = configuration
                .GetSection(IdentitySettings.SectionName)
                .Get<IdentitySettings>() ?? new IdentitySettings();

            // Password policy
            options.Password.RequiredLength = identity.Password.RequiredLength;
            options.Password.RequireDigit = identity.Password.RequireDigit;
            options.Password.RequireLowercase = identity.Password.RequireLowercase;
            options.Password.RequireUppercase = identity.Password.RequireUppercase;
            options.Password.RequireNonAlphanumeric = identity.Password.RequireNonAlphanumeric;

            // Lockout policy
            options.Lockout.MaxFailedAccessAttempts = identity.Lockout.MaxFailedAttempts;
            options.Lockout.DefaultLockoutTimeSpan = identity.Lockout.LockoutDuration;
            options.Lockout.AllowedForNewUsers = true;

            // Sign-in policy — phone confirmation is the gate
            options.SignIn.RequireConfirmedPhoneNumber = false;  // phone is auto-confirmed on first OTP verify
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedAccount = false;

            // User policy
            options.User.RequireUniqueEmail = false;
        })
        .AddEntityFrameworkStores<BineshDbContext>()
        .AddDefaultTokenProviders();

        // ── JWT signing certificate (lazy; dev fallback in-memory) ────────────
        services.AddSingleton(sp =>
        {
            var jwt = sp.GetRequiredService<IOptions<JwtSettings>>().Value;
            var env = sp.GetRequiredService<IHostEnvironment>();
            var logger = sp.GetRequiredService<ILogger<JwtSettings>>();

            if (string.IsNullOrWhiteSpace(jwt.SigningCertificatePath))
            {
                if (!env.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "JWT signing certificate is not configured. Set Jwt:SigningCertificatePath "
                        + "and Jwt:SigningCertificatePassword via env vars.");
                }

                logger.LogWarning(
                    "No JWT signing certificate configured. Generating ephemeral self-signed cert "
                    + "for development. Tokens will be invalidated on restart.");

                return GenerateEphemeralDevCertificate();
            }

            var flags = OperatingSystem.IsWindows()
                ? X509KeyStorageFlags.EphemeralKeySet
                : X509KeyStorageFlags.DefaultKeySet;

            var cert = X509CertificateLoader.LoadPkcs12FromFile(
                jwt.SigningCertificatePath,
                jwt.SigningCertificatePassword ?? string.Empty,
                flags);

            if (cert.NotAfter < DateTime.UtcNow)
            {
                throw new InvalidOperationException(
                    $"JWT signing certificate has expired at {cert.NotAfter:u}");
            }

            return cert;
        });

        services.AddSingleton(sp =>
        {
            var jwt = sp.GetRequiredService<IOptions<JwtSettings>>().Value;
            var cert = sp.GetRequiredService<X509Certificate2>();

            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new X509SecurityKey(cert),
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role,
            };
        });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(_ => { });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TokenValidationParameters>((opts, tvp) =>
            {
                opts.TokenValidationParameters = tvp;
                opts.MapInboundClaims = false;
            });

        // ── Authorization policies ───────────────────────────────────────────
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorizationBuilder()
            .AddRolePolicies()
            .AddPermissionPolicies();

        services.AddHttpContextAccessor();

        // ── Bootstrap (roles + SuperAdmin seed) ──────────────────────────────
        services.AddHostedService<IdentityBootstrapService>();

        // ── Domain services ──────────────────────────────────────────────────
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IOtpService, OtpService>();

        // ── SMS sender (picked by Sms:Provider) ──────────────────────────────
        services.AddHttpClient();
        services.AddScoped<ISmsSender>(sp =>
        {
            var provider = sp.GetRequiredService<IOptions<SmsSettings>>().Value.Provider;
            return provider.Equals("ippanel", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<IppanelSmsSender>(sp)
                : ActivatorUtilities.CreateInstance<LogSmsSender>(sp);
        });

        return services;
    }

    private static AuthorizationBuilder AddRolePolicies(this AuthorizationBuilder builder)
    {
        // role:SuperAdmin — exclusive SuperAdmin actions (create / delete users).
        builder.AddPolicy("role:SuperAdmin", policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(AppRoles.SuperAdmin));

        // role:Admin — SuperAdmin OR Admin. Read endpoints, anything not SuperAdmin-exclusive.
        builder.AddPolicy("role:Admin", policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(AppRoles.SuperAdmin, AppRoles.Admin));

        return builder;
    }

    private static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var permission in PermissionClaims.All)
        {
            builder.AddPolicy($"permission:{permission}", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(permission));
            });
        }
        return builder;
    }

    private static X509Certificate2 GenerateEphemeralDevCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=binesh-dev-jwt-signing",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        var bytes = cert.Export(X509ContentType.Pfx, password: string.Empty);
        return X509CertificateLoader.LoadPkcs12(bytes, password: string.Empty);
    }
}
