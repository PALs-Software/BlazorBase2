namespace BlazorBase.User.Server.AccessTokens.BruteForce;

/// <summary>
/// Applies per-client exponential back-off delays to defend against brute-force token guessing.
/// </summary>
public interface IBruteForceDelayService
{
    /// <summary>
    /// Waits for the current back-off delay associated with <paramref name="clientKey"/> (e.g. remote IP).
    /// Returns immediately when no failures are recorded.
    /// </summary>
    Task GetDelayAsync(string clientKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the back-off delay in milliseconds for a given failure count, capped at the configured maximum.
    /// Pure function with no side effects or waiting; exposed for deterministic testing.
    /// </summary>
    int CalculateDelayMilliseconds(int failures);

    /// <summary>Increments the failure counter for <paramref name="clientKey"/>, extending the next delay.</summary>
    void RegisterFailure(string clientKey);

    /// <summary>Resets the failure counter for <paramref name="clientKey"/>.</summary>
    void RegisterSuccess(string clientKey);
}
