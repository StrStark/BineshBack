using Binesh.Identity.Features.Users.Shared;
using MediatR;

namespace Binesh.Identity.Features.Users.CreateUser;

/// <summary>
/// Creates a new Admin user. Only callable by SuperAdmin (enforced at endpoint).
/// Role is hardcoded to Admin for now — more roles arrive later when the
/// frontend's permission model expands.
///
/// The new user does NOT receive an OTP automatically. They sign in via the
/// normal flow: POST /api/auth/otp/request with their phone, then /verify.
/// </summary>
public sealed record CreateUserCommand(
    string PhoneNumber,
    string? FirstName,
    string? LastName,
    string? JobTitle)
    : IRequest<UserDto>;
