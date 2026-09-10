using System.Security.Claims;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// The framework's safe default <see cref="IExternalUserResolver{TUser}"/>: matches an external
/// identity to a local account through ASP.NET Core Identity's own external-login table
/// (<see cref="UserManager{TUser}.FindByLoginAsync(string, string)"/>), which already exists in
/// every host's schema and is keyed by provider subject, not by anything a token issuer leaves to
/// the caller's discretion. <c>providerKey</c> is the first non-empty value found among
/// <see cref="SubjectClaimTypes"/>; none present rejects outright. Never <c>email</c>,
/// <c>preferred_username</c>, <c>upn</c> or <c>name</c> - none of those are stable, provider-issued
/// identifiers, and a couple of them are not even guaranteed to be verified by the issuer. Linking
/// an account is a host decision this resolver does not make on its own: a host writes the login
/// row itself via <see cref="UserManager{TUser}.AddLoginAsync(TUser, UserLoginInfo)"/>, so an
/// unrecognised subject is rejected rather than auto-provisioned or matched by anything weaker.
/// </summary>
public class IdentityLoginExternalUserResolver<TUser>(
    UserManager<TUser> userManager,
    IOptions<ExternalAuthSettings> settings) : IExternalUserResolver<TUser>
    where TUser : BaseUser
{
    /// <summary>
    /// The four claim-type names that identify only two claims, <c>oid</c> and <c>sub</c>, in the
    /// order to search: a raw JWT presented to <see cref="OidcExternalTokenValidator"/> (the
    /// token-exchange flow) carries the short names verbatim, but a real <c>AddOpenIdConnect</c>
    /// handler (the redirect flow) applies ASP.NET Core's default inbound claim-type mapping, which
    /// renames <c>sub</c> to <see cref="ClaimTypes.NameIdentifier"/> and <c>oid</c> to the long
    /// Microsoft object-identifier URI before either short name ever reaches this resolver. Both
    /// entry points hand it the same <see cref="ClaimsPrincipal"/> shape otherwise, so all four names
    /// must be tried - dropping any one of them silently breaks whichever entry point produced that
    /// name.
    /// </summary>
    private static readonly IReadOnlyList<string> SubjectClaimTypes =
    [
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        "sub",
        ClaimTypes.NameIdentifier,
    ];

    private readonly ExternalAuthSettings Settings = settings.Value;

    public async Task<ExternalUserResolution<TUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken)
    {
        var providerKey = FindSubject(externalPrincipal);

        if (providerKey is null)
            return ExternalUserResolution<TUser>.Reject("The external token carries no subject claim under any of the recognised oid/sub claim types.");

        var user = await userManager.FindByLoginAsync(Settings.LoginProvider, providerKey);

        return user is null
            ? ExternalUserResolution<TUser>.Reject($"No local account is linked to subject '{providerKey}' under login provider '{Settings.LoginProvider}'.")
            : ExternalUserResolution<TUser>.Accept(user);
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
