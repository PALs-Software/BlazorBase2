namespace BlazorBase.User.Server.Services;

/// <summary>
/// Tracks which two-factor pending-token ids have already completed a successful
/// <c>login/2fa</c>, so a token cannot mint a second session once it has already minted one.
/// </summary>
/// <remarks>
/// The default <see cref="InMemoryTwoFactorPendingTokenStore"/> registered by this framework is
/// per-process: an id consumed on one node of a multi-instance host is not visible to another node,
/// so the same pending token can still be replayed against a different instance. A host running more
/// than one instance behind a load balancer must register its own distributed implementation (for
/// example backed by a shared cache) to close that gap.
/// </remarks>
public interface ITwoFactorPendingTokenStore
{
    /// <summary>Whether <paramref name="tokenId"/> has already completed a successful login.</summary>
    Task<bool> IsConsumedAsync(string tokenId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks <paramref name="tokenId"/> as consumed. Returns <see langword="true"/> the first time a
    /// given id is consumed, <see langword="false"/> if it was already consumed before.
    /// </summary>
    /// <param name="expiresUtc">
    /// The pending token's own expiry, so the store can forget <paramref name="tokenId"/> once it
    /// could no longer be presented anyway.
    /// </param>
    Task<bool> TryConsumeAsync(string tokenId, DateTimeOffset expiresUtc, CancellationToken cancellationToken);
}
