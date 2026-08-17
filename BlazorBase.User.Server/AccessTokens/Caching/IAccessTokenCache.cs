namespace BlazorBase.User.Server.AccessTokens.Caching;

/// <summary>
/// In-memory, read-through cache of access tokens keyed by their non-secret prefix. Populated
/// lazily, one prefix at a time, by whichever caller first looks up that prefix - there is no
/// eager full-table preload, so a token created by another process (a second instance, an operator
/// CLI) becomes usable on its very first validation attempt rather than only after a restart.
/// </summary>
public interface IAccessTokenCache
{
    /// <summary>
    /// Attempts to retrieve all candidate tokens that share <paramref name="prefix"/> from the
    /// cache alone. A <see langword="false"/> result means "not cached yet", not "does not exist" -
    /// callers must fall back to the database and populate the cache via <see cref="Set"/>.
    /// </summary>
    bool TryGetByPrefix(string prefix, out IReadOnlyList<CachedAccessToken> candidates);

    /// <summary>Adds or replaces the cached entry for the given token.</summary>
    void Set(CachedAccessToken token);

    /// <summary>Removes the cached entry for the token identified by <paramref name="tokenId"/>.</summary>
    void Remove(Guid tokenId);

    /// <summary>Discards every cached entry.</summary>
    void Clear();
}
