using Binesh.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Binesh.Identity.Services;

/// <summary>
/// Wraps <see cref="UserManager{TUser}"/>'s phone token provider so the rest
/// of the codebase doesn't depend on Identity directly.
///
/// Replaces the old code's ad-hoc <c>FormattableString.Invariant($"VerifyPhoneNumber:{...}")</c>
/// purpose string scattered through the AuthController.
/// </summary>
internal sealed class OtpService(UserManager<User> userManager) : IOtpService
{
    private const string Purpose = "phone-otp";

    public Task<string> GenerateAsync(User user) =>
        userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, Purpose);

    public Task<bool> VerifyAsync(User user, string otp) =>
        userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, Purpose, otp);
}
