using System.Security.Claims;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.Server.ExternalAuth;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>
/// Demonstrates the account-takeover risk the framework's missing default resolver leaves open: it
/// matches an external identity to a local user purely by the token's <c>email</c> claim, without
/// ever checking <c>email_verified</c>. This exists so the harness's probe catalogue can prove
/// whether that risk is actually exploitable rather than merely asserting it - it is not a
/// recommendation and must never be copied into an application.
/// </summary>
internal sealed class NaiveEmailMatchingExternalUserResolver(UserManager<HarnessUser> users) : IExternalUserResolver<HarnessUser>
{
    public async Task<ExternalUserResolution<HarnessUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken)
    {
        var email = externalPrincipal.FindFirst("email")?.Value;

        if (email is null)
            return ExternalUserResolution<HarnessUser>.Reject("The external token carries no email claim.");

        var user = await users.FindByEmailAsync(email);

        return user is null
            ? ExternalUserResolution<HarnessUser>.Reject($"No local user is registered for email '{email}'.")
            : ExternalUserResolution<HarnessUser>.Accept(user);
    }
}
