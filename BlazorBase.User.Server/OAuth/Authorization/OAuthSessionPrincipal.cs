using System.Security.Claims;
using BlazorBase.User.Server.AccessTokens;

namespace BlazorBase.User.Server.OAuth.Authorization;

/// <summary>
/// Tells the application's own signed-in session apart from a principal authenticated by an access
/// token. Only the session may decide on consent or manage grants: an access token, above all one an
/// OAuth client holds, must not be able to authorize further clients or list and revoke its user's grants.
/// </summary>
internal static class OAuthSessionPrincipal
{
    /// <summary>
    /// Returns the user id of a session principal, or <see langword="null"/> for an access-token principal
    /// or a principal without a user id.
    /// </summary>
    public static string? FindUserId(ClaimsPrincipal user)
    {
        if (user.HasClaim(claim => claim.Type == AccessTokenClaimTypes.TokenId))
            return null;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId) ? null : userId;
    }
}
