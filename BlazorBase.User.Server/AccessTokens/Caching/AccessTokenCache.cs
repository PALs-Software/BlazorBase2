using System.Collections.Concurrent;

namespace BlazorBase.User.Server.AccessTokens.Caching;

/// <summary>
/// Singleton in-memory cache of access tokens, keyed by their non-secret prefix.
/// Each prefix maps to a candidate list to handle the rare case of prefix collisions.
/// </summary>
/// <remarks>
/// A cache miss on <see cref="TryGetByPrefix"/> never inserts anything by itself - it is a plain
/// <see cref="ConcurrentDictionary{TKey,TValue}.TryGetValue"/> lookup. Only <see cref="Set"/>
/// (called after a genuine database hit for that prefix) grows the dictionary, so an attacker
/// probing with random secrets - each producing a distinct, non-existent prefix - adds no entries
/// at all, however many attempts are made. Without that property the cache would be an unbounded
/// memory sink addressable by anyone who can reach the endpoint.
/// </remarks>
public class AccessTokenCache : IAccessTokenCache
{
    private readonly ConcurrentDictionary<string, List<CachedAccessToken>> TokensByPrefix = new(StringComparer.Ordinal);

    public bool TryGetByPrefix(string prefix, out IReadOnlyList<CachedAccessToken> candidates)
    {
        if (!TokensByPrefix.TryGetValue(prefix, out var list))
        {
            candidates = [];
            return false;
        }

        candidates = list;
        return true;
    }

    public void Set(CachedAccessToken token)
    {
        TokensByPrefix.AddOrUpdate(
            token.Prefix,
            _ => [token],
            (_, existing) => existing.Where(entry => entry.Id != token.Id).Append(token).ToList());
    }

    public void Remove(Guid tokenId)
    {
        foreach (var (prefix, candidates) in TokensByPrefix)
        {
            var filtered = candidates.Where(entry => entry.Id != tokenId).ToList();

            if (filtered.Count == 0)
                TokensByPrefix.TryRemove(prefix, out _);
            else
                TokensByPrefix[prefix] = filtered;
        }
    }

    public void Clear()
    {
        TokensByPrefix.Clear();
    }
}
