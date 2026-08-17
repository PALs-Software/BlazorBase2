namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Application service for the full access-token lifecycle: creation, validation, revocation, and
/// listing.
/// </summary>
/// <remarks>
/// Every owner-aware method takes an optional <c>userId</c>. Passing one restricts the operation to
/// that account's tokens; passing <see langword="null"/> operates across all of them. An
/// application whose tokens belong to accounts must pass the current user's id on
/// <see cref="ListAsync"/> and <see cref="RevokeAsync"/> - omitting it there is what turns a
/// per-user token list into an everyone's-tokens list.
/// </remarks>
public interface IAccessTokenService
{
    /// <summary>
    /// Creates a new access token, persists its hash, and returns its identity together with the
    /// one-time plain-text secret.
    /// </summary>
    Task<CreatedAccessToken> CreateAsync(string name, DateTime? expiresAt, string? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a raw secret, checking expiry and revocation against current database state.
    /// Returns a <see cref="ValidatedAccessToken"/> on success; <see langword="null"/> on any failure.
    /// </summary>
    Task<ValidatedAccessToken?> ValidateAsync(string secret, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the token identified by <paramref name="tokenId"/>, optionally only when it belongs
    /// to <paramref name="userId"/>. Returns <see langword="false"/> when no matching token exists.
    /// </summary>
    Task<bool> RevokeAsync(Guid tokenId, string? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns access tokens ordered by name, restricted to <paramref name="userId"/> when given.
    /// </summary>
    Task<IReadOnlyList<AccessTokenSummary>> ListAsync(string? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the <c>LastUsedAt</c> timestamp for the given token, throttled so a busy token does
    /// not cause a write per request. Best-effort - failures are silently ignored.
    /// </summary>
    Task TouchAsync(Guid tokenId, CancellationToken cancellationToken = default);
}
