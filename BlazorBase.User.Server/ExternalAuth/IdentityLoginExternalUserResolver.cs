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
/// the caller's discretion. <c>providerKey</c> is the token's <c>oid</c> claim if present,
/// otherwise its <c>sub</c> claim; neither present rejects outright. Never <c>email</c>,
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
    private readonly ExternalAuthSettings Settings = settings.Value;

    public async Task<ExternalUserResolution<TUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken)
    {
        var providerKey = externalPrincipal.FindFirst("oid")?.Value ?? externalPrincipal.FindFirst("sub")?.Value;

        if (providerKey is null)
            return ExternalUserResolution<TUser>.Reject("The external token carries neither an oid nor a sub claim.");

        var user = await userManager.FindByLoginAsync(Settings.LoginProvider, providerKey);

        return user is null
            ? ExternalUserResolution<TUser>.Reject($"No local account is linked to subject '{providerKey}' under login provider '{Settings.LoginProvider}'.")
            : ExternalUserResolution<TUser>.Accept(user);
    }
}
