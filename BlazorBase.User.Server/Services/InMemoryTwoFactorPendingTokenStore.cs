using System.Collections.Concurrent;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Default, per-process <see cref="ITwoFactorPendingTokenStore"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. See the interface's own remarks for why a
/// multi-instance host needs a distributed replacement.
/// </summary>
/// <remarks>
/// Consumed ids are pruned once their token's own expiry has passed. The prune runs
/// opportunistically inside <see cref="TryConsumeAsync"/> rather than on a dedicated background
/// sweep, so the store never grows without bound but also never needs a hosted service of its own.
/// </remarks>
public sealed class InMemoryTwoFactorPendingTokenStore(TimeProvider timeProvider) : ITwoFactorPendingTokenStore
{
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> ConsumedTokenExpiries = new(StringComparer.Ordinal);

    public Task<bool> IsConsumedAsync(string tokenId, CancellationToken cancellationToken) =>
        Task.FromResult(ConsumedTokenExpiries.ContainsKey(tokenId));

    public Task<bool> TryConsumeAsync(string tokenId, DateTimeOffset expiresUtc, CancellationToken cancellationToken)
    {
        PruneExpiredEntries();

        return Task.FromResult(ConsumedTokenExpiries.TryAdd(tokenId, expiresUtc));
    }

    private void PruneExpiredEntries()
    {
        var now = TimeProvider.GetUtcNow();

        foreach (var (tokenId, expiresUtc) in ConsumedTokenExpiries)
        {
            if (expiresUtc < now)
                ConsumedTokenExpiries.TryRemove(tokenId, out _);
        }
    }
}
