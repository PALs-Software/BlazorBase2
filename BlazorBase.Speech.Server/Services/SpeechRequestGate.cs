using BlazorBase.Speech.Server.Configuration;
using Microsoft.Extensions.Options;

namespace BlazorBase.Speech.Server.Services;

/// <summary>
/// Caps how many speech requests one user may have in flight. Transcription and synthesis are
/// CPU-heavy on the speech service; without a cap one busy tab could starve everyone else.
/// </summary>
public class SpeechRequestGate(IOptions<SpeechServerOptions> options)
{
    #region Injects
    private readonly IOptions<SpeechServerOptions> Options = options;
    #endregion

    private readonly Dictionary<string, int> InFlightRequests = new(StringComparer.Ordinal);
    private readonly Lock SyncRoot = new();

    /// <summary>Claims a slot for <paramref name="userKey"/>.</summary>
    /// <returns>A lease that frees the slot when disposed, or <see langword="null"/> when the user is at the limit.</returns>
    public IDisposable? TryEnter(string userKey)
    {
        lock (SyncRoot)
        {
            var current = InFlightRequests.GetValueOrDefault(userKey);

            if (current >= Options.Value.MaxConcurrentRequestsPerUser)
                return null;

            InFlightRequests[userKey] = current + 1;
        }

        return new SpeechRequestLease(() => Exit(userKey));
    }

    private void Exit(string userKey)
    {
        lock (SyncRoot)
        {
            var remaining = InFlightRequests.GetValueOrDefault(userKey) - 1;

            if (remaining <= 0)
                InFlightRequests.Remove(userKey);
            else
                InFlightRequests[userKey] = remaining;
        }
    }
}
