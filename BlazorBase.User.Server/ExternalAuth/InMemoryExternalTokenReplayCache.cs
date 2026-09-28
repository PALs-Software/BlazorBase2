using System.Collections.Concurrent;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Default, per-process <see cref="IExternalTokenReplayCache"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. See the interface's own remarks for why a
/// multi-instance host needs a distributed replacement.
/// </summary>
/// <remarks>
/// Spent keys are pruned once their own retention window has passed. The prune runs
/// opportunistically inside <see cref="TryRegisterAsync"/> rather than on a dedicated background
/// sweep, so the cache never grows without bound but also never needs a hosted service of its
/// own.
/// </remarks>
public sealed class InMemoryExternalTokenReplayCache(TimeProvider timeProvider) : IExternalTokenReplayCache
{
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> SpentKeyExpiries = new(StringComparer.Ordinal);

    public Task<bool> TryRegisterAsync(string key, DateTimeOffset expiresUtc, CancellationToken cancellationToken)
    {
        PruneExpiredEntries();

        return Task.FromResult(SpentKeyExpiries.TryAdd(key, expiresUtc));
    }

    private void PruneExpiredEntries()
    {
        var now = TimeProvider.GetUtcNow();

        foreach (var (key, expiresUtc) in SpentKeyExpiries)
        {
            if (expiresUtc < now)
                SpentKeyExpiries.TryRemove(key, out _);
        }
    }
}
