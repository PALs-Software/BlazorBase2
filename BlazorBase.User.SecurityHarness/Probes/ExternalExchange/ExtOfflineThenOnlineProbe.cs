using System.Diagnostics;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// A measurement, not a verdict: whether a provider outage encountered on a cold configuration
/// cache leaves a lock-out behind once the provider recovers, and for how long. Exists because step
/// 2 observed a bootstrap back-off that appeared to reject tokens that would otherwise validate; this
/// probe confirms or kills that observation with a real, bounded measurement instead of leaving it as
/// a suspicion.
/// </summary>
internal sealed class ExtOfflineThenOnlineProbe : IAuthProbe
{
    private const int MaxRecoveryAttempts = 8;
    private static readonly TimeSpan DelayBetweenAttempts = TimeSpan.FromMilliseconds(250);

    public string Id => "EXT-OFFLINE-THEN-ONLINE";
    public string Category => "External token exchange";
    public string Attack => "One request while the provider is down against a cold configuration cache, then a valid request immediately after it recovers.";
    public string Expectation => "A measurement, not a verdict: whether the earlier failure locks out the now-healthy provider, and for how long.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Info;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var providerHost = ExternalExchangeSupport.GetProviderHost(context);

        try
        {
            providerHost.GoOffline();

            await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
            using var client = host.Context.HttpClientFactory();
            await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

            var description = ExternalExchangeSupport.CreateValidDescription(providerHost);
            var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

            var whileOffline = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
            var evidence = new List<ProbeEvidence> { ExternalExchangeSupport.BuildEvidence("A request against a cold configuration cache, while the provider is offline.", token, whileOffline) };

            providerHost.GoOnline();
            var stopwatch = Stopwatch.StartNew();

            var recoveryAttempt = 0;

            for (var attempt = 1; attempt <= MaxRecoveryAttempts; attempt++)
            {
                var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
                evidence.Add(ExternalExchangeSupport.BuildEvidence($"Recovery attempt {attempt} of {MaxRecoveryAttempts}, immediately after the provider came back online.", token, exchange));

                if (exchange.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    recoveryAttempt = attempt;
                    break;
                }

                if (attempt < MaxRecoveryAttempts)
                    await Task.Delay(DelayBetweenAttempts, cancellationToken);
            }

            stopwatch.Stop();

            var recoverySummary = recoveryAttempt == 0
                ? $"did not recover within {MaxRecoveryAttempts} attempts ({stopwatch.ElapsedMilliseconds} ms)"
                : $"recovered on attempt {recoveryAttempt} of {MaxRecoveryAttempts}, {stopwatch.ElapsedMilliseconds} ms after the provider came back online";

            return ProbeResult.Inconclusive(
                $"Measurement: while offline, the request answered {(int)whileOffline.StatusCode} {whileOffline.StatusCode}; after the provider recovered, the same valid token {recoverySummary}.",
                evidence);
        }
        finally
        {
            providerHost.GoOnline();
        }
    }
}
