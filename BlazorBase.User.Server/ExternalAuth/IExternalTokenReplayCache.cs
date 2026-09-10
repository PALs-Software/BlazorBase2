namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Tracks which external tokens have already been exchanged for a local session at
/// <c>api/auth/external/exchange</c>, so the same provider token cannot mint more than one
/// session for as long as it remains valid.
/// </summary>
/// <remarks>
/// The default <see cref="InMemoryExternalTokenReplayCache"/> registered by this framework is
/// per-process: a token spent on one node of a multi-instance host is not visible to another
/// node, so the same token can still be replayed against a different instance. A host running
/// more than one instance behind a load balancer must register its own distributed
/// implementation (for example backed by a shared cache) to close that gap.
/// </remarks>
public interface IExternalTokenReplayCache
{
    /// <summary>
    /// Attempts to record <paramref name="key"/> as spent. Returns <see langword="true"/> the
    /// first time a given key is presented, and <see langword="false"/> on every subsequent call
    /// for the same key while it has not yet expired.
    /// </summary>
    /// <param name="expiresUtc">
    /// The end of the window in which the underlying token could still be presented again, so the
    /// cache can forget <paramref name="key"/> once it could no longer be replayed anyway.
    /// </param>
    Task<bool> TryRegisterAsync(string key, DateTimeOffset expiresUtc, CancellationToken cancellationToken);
}
