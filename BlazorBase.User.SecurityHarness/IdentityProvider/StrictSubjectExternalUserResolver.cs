using System.Security.Claims;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.Server.ExternalAuth;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>
/// Matches an external identity by its provider subject (<c>oid</c>, falling back to <c>sub</c>)
/// against <see cref="BlazorBase.User.Server.Entities.BaseUser.UserName"/>, rejecting any subject
/// with no matching local user. The framework ships no default resolver at all - this is what a
/// host that does the safe thing looks like.
/// </summary>
internal sealed class StrictSubjectExternalUserResolver(UserManager<HarnessUser> users) : IExternalUserResolver<HarnessUser>
{
    public async Task<ExternalUserResolution<HarnessUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken)
    {
        var subject = externalPrincipal.FindFirst("oid")?.Value ?? externalPrincipal.FindFirst("sub")?.Value;

        if (subject is null)
            return ExternalUserResolution<HarnessUser>.Reject("The external token carries neither an oid nor a sub claim.");

        var user = await users.FindByNameAsync(subject);

        return user is null
            ? ExternalUserResolution<HarnessUser>.Reject($"No local user is registered for subject '{subject}'.")
            : ExternalUserResolution<HarnessUser>.Accept(user);
    }
}
