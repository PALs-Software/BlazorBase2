using System.Collections.Concurrent;

namespace BlazorBase.User.Server.Security;

/// <summary>
/// Remembers, per user id, when <see cref="IAccessTokenStampValidator"/> last confirmed a caller's
/// stamp was current - so a non-zero <see cref="AccessTokenStampValidationOptions.Interval"/> can skip
/// the database read on every request in between, trading freshness for load exactly as that option's
/// documentation states.
/// </summary>
public sealed class AccessTokenStampValidationCache
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> LastValidatedUtc = new();

    /// <summary>
    /// <see langword="true"/> when <paramref name="userId"/> was validated within <paramref name="interval"/>
    /// of <paramref name="timeProvider"/>'s current time - always <see langword="false"/> for a zero or
    /// negative interval, so the default (validate every request) never consults the cache at all.
    /// </summary>
    public bool WasRecentlyValidated(string userId, TimeSpan interval, TimeProvider timeProvider) =>
        interval > TimeSpan.Zero
        && LastValidatedUtc.TryGetValue(userId, out var lastValidated)
        && timeProvider.GetUtcNow() - lastValidated < interval;

    public void MarkValidated(string userId, TimeProvider timeProvider) =>
        LastValidatedUtc[userId] = timeProvider.GetUtcNow();
}
