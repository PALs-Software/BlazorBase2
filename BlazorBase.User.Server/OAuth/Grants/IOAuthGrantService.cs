using BlazorBase.User.Models;

namespace BlazorBase.User.Server.OAuth.Grants;

/// <summary>
/// The connected apps of a user: every <see cref="Entities.OAuthGrant"/> they approved, and the way to
/// disconnect one. Backs <c>GET</c>/<c>DELETE api/oauth/grants</c>; a host may also call it directly,
/// for example to revoke every grant of an account it disables.
/// </summary>
public interface IOAuthGrantService
{
    /// <summary>Returns the user's grants that are not revoked, newest first.</summary>
    Task<IReadOnlyList<OAuthGrantSummary>> ListAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the grant, optionally only when it belongs to <paramref name="userId"/>: sets
    /// <c>RevokedAt</c>, consumes every refresh token and revokes every access token issued under it,
    /// effective on the very next request. Returns <see langword="false"/> when no matching active grant exists.
    /// </summary>
    Task<bool> RevokeAsync(Guid grantId, string? userId = null, CancellationToken cancellationToken = default);
}
