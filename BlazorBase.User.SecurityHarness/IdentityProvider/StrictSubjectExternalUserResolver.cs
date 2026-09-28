using System.Security.Claims;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.Server.ExternalAuth;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.SecurityHarness.IdentityProvider;

/// <summary>
/// Matches an external identity by its provider subject against
/// <see cref="BlazorBase.User.Server.Entities.BaseUser.UserName"/>, rejecting any subject with no
/// matching local user. The framework ships no default resolver at all - this is what a host that
/// does the safe thing looks like.
/// </summary>
internal sealed class StrictSubjectExternalUserResolver(UserManager<HarnessUser> users) : IExternalUserResolver<HarnessUser>
{
    /// <summary>
    /// The four claim-type names that identify only two claims, <c>oid</c> and <c>sub</c>: the
    /// token-exchange flow's raw JWT carries the short names verbatim, but the redirect flow's real
    /// <c>AddOpenIdConnect</c> handler applies ASP.NET Core's default inbound claim-type mapping,
    /// which renames <c>sub</c> to <see cref="ClaimTypes.NameIdentifier"/> and <c>oid</c> to the long
    /// Microsoft object-identifier URI before this resolver ever sees the principal. Kept identical
    /// to <c>IdentityLoginExternalUserResolver.SubjectClaimTypes</c> in
    /// <c>BlazorBase.User.Server/ExternalAuth/</c> so the harness measures the endpoints under test
    /// rather than its own, narrower claim-name assumption.
    /// </summary>
    private static readonly IReadOnlyList<string> SubjectClaimTypes =
    [
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        "sub",
        ClaimTypes.NameIdentifier,
    ];

    public async Task<ExternalUserResolution<HarnessUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken)
    {
        var subject = FindSubject(externalPrincipal);

        if (subject is null)
            return ExternalUserResolution<HarnessUser>.Reject("The external token carries no subject claim under any of the recognised oid/sub claim types.");

        var user = await users.FindByNameAsync(subject);

        return user is null
            ? ExternalUserResolution<HarnessUser>.Reject($"No local user is registered for subject '{subject}'.")
            : ExternalUserResolution<HarnessUser>.Accept(user);
    }

    private static string? FindSubject(ClaimsPrincipal externalPrincipal)
    {
        foreach (var claimType in SubjectClaimTypes)
        {
            var value = externalPrincipal.FindFirst(claimType)?.Value;

            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }
}
