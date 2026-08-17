using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.AccessTokens.BruteForce;

/// <summary>
/// Singleton service that tracks per-client failure counts and applies an exponential capped delay
/// before each authentication attempt that follows a previous failure.
/// </summary>
/// <remarks>
/// The client key handed in by <see cref="AccessTokenAuthenticationHandler"/> is always the
/// connection's <c>RemoteIpAddress</c>, never a client-suppliable header such as
/// <c>X-Forwarded-For</c> - a caller able to choose its own bucket could reset the delay at will
/// and defeat the whole mechanism. Behind a reverse proxy, the host is responsible for calling
/// <c>UseForwardedHeaders</c> with a trusted-proxy list, which makes <c>RemoteIpAddress</c> resolve
/// to the real client without any change here.
///
/// State is per-process. With several instances behind a load balancer, an attacker gets one
/// independent back-off ladder per instance; treat this as a brake on guessing, not as a global
/// rate limit, and pair it with one at the edge if that matters.
/// </remarks>
public class BruteForceDelayService(IOptions<AccessTokenSettings> options) : IBruteForceDelayService
{
    #region Injects
    private readonly AccessTokenSettings Settings = options.Value;
    #endregion

    private readonly ConcurrentDictionary<string, (int Failures, DateTimeOffset LastFailureAt)> FailureRecords
        = new(StringComparer.OrdinalIgnoreCase);

    public async Task GetDelayAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        PurgeStaleEntries();

        if (!FailureRecords.TryGetValue(clientKey, out var record) || record.Failures <= 0)
            return;

        await Task.Delay(CalculateDelayMilliseconds(record.Failures), cancellationToken);
    }

    public int CalculateDelayMilliseconds(int failures)
    {
        if (failures <= 0)
            return 0;

        var shift = Math.Min(failures - 1, 30);
        return (int)Math.Min((long)Settings.BruteForceBaseDelayMs << shift, Settings.BruteForceMaxDelayMs);
    }

    public void RegisterFailure(string clientKey)
    {
        FailureRecords.AddOrUpdate(
            clientKey,
            _ => (1, DateTimeOffset.UtcNow),
            (_, current) => (current.Failures + 1, DateTimeOffset.UtcNow));
    }

    public void RegisterSuccess(string clientKey)
    {
        FailureRecords.TryRemove(clientKey, out _);
    }

    private void PurgeStaleEntries()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMilliseconds(-(2.0 * Settings.BruteForceMaxDelayMs));

        foreach (var (key, record) in FailureRecords)
        {
            if (record.LastFailureAt < cutoff)
                FailureRecords.TryRemove(key, out _);
        }
    }
}
