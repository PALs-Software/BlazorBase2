using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using Microsoft.IdentityModel.Protocols;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// A measurement, not a verdict: how many requests it takes, in each of the two supported
/// <see cref="BaseConfigurationManager"/> refresh modes, before a token signed with the provider's
/// newly rotated key is accepted. Runs on its own isolated host per mode - so each measurement
/// starts from a cold <see cref="BaseConfigurationManager"/> cache - and restores the provider's key
/// registry and the process-wide refresh-mode switch to their shipping-default state afterward, since
/// both are shared with every other probe against this profile.
/// </summary>
internal sealed class ExtKeyRotationProbe : IAuthProbe
{
    private const int MaxAttempts = 6;
    private static readonly TimeSpan DelayBetweenAttempts = TimeSpan.FromMilliseconds(150);

    public string Id => "EXT-KEY-ROTATION";
    public string Category => "External token exchange";
    public string Attack => "Withholds key A, publishes key B, then presents a token signed with B - once with the library's default fire-and-forget refresh, once with blocking refresh.";
    public string Expectation => "A measurement, not a verdict: how many requests it takes to be accepted, in each refresh mode.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Info;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var providerHost = ExternalExchangeSupport.GetProviderHost(context);

        try
        {
            var evidence = new List<ProbeEvidence>();

            var defaultModeAttempts = await MeasureRotationAsync(context, providerHost, useBlockingRefresh: false, evidence, cancellationToken);
            var blockingModeAttempts = await MeasureRotationAsync(context, providerHost, useBlockingRefresh: true, evidence, cancellationToken);

            return ProbeResult.Inconclusive(
                $"Measurement: with the default fire-and-forget refresh, the rotated key was accepted on {DescribeAttempts(defaultModeAttempts)}; with blocking refresh, on {DescribeAttempts(blockingModeAttempts)}.",
                evidence);
        }
        finally
        {
            providerHost.KeyRegistry.Publish(FakeSigningKeyRegistry.KeyIdA);
            providerHost.KeyRegistry.Withhold(FakeSigningKeyRegistry.KeyIdB);
            PinnedThumbprintConfigurationManagerFactory.SetBlockingRefreshMode(false);
        }
    }

    private static async Task<int?> MeasureRotationAsync(ProbeContext context, FakeIdentityProviderHost providerHost, bool useBlockingRefresh, List<ProbeEvidence> evidence, CancellationToken cancellationToken)
    {
        providerHost.KeyRegistry.Publish(FakeSigningKeyRegistry.KeyIdA);
        providerHost.KeyRegistry.Withhold(FakeSigningKeyRegistry.KeyIdB);
        PinnedThumbprintConfigurationManagerFactory.SetBlockingRefreshMode(useBlockingRefresh);

        var modeLabel = useBlockingRefresh ? "blocking" : "default fire-and-forget";
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var primeDescription = ExternalExchangeSupport.CreateValidDescription(providerHost);
        var primeToken = ExternalExchangeSupport.SignWithKeyA(providerHost, primeDescription);
        var primeExchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, primeToken, cancellationToken);
        evidence.Add(ExternalExchangeSupport.BuildEvidence($"[{modeLabel}] Priming request, signed with key A before rotation.", primeToken, primeExchange));

        if (primeExchange.StatusCode != System.Net.HttpStatusCode.OK)
            return null;

        PinnedThumbprintConfigurationManagerFactory.RotateSigningKey(providerHost.KeyRegistry, FakeSigningKeyRegistry.KeyIdB, FakeSigningKeyRegistry.KeyIdA, useBlockingRefresh);

        var rotatedDescription = ExternalExchangeSupport.CreateValidDescription(providerHost);
        var rotatedToken = FakeTokenBuilder.CreateRsaSignedToken(rotatedDescription, FakeSigningKeyRegistry.KeyIdB, providerHost.KeyRegistry.GetKey(FakeSigningKeyRegistry.KeyIdB));

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, rotatedToken, cancellationToken);
            evidence.Add(ExternalExchangeSupport.BuildEvidence($"[{modeLabel}] Attempt {attempt} of {MaxAttempts} after rotation, signed with the newly published key B.", rotatedToken, exchange));

            if (exchange.StatusCode == System.Net.HttpStatusCode.OK)
                return attempt;

            if (attempt < MaxAttempts)
                await Task.Delay(DelayBetweenAttempts, cancellationToken);
        }

        return null;
    }

    private static string DescribeAttempts(int? attempts) =>
        attempts is null ? $"not accepted within {MaxAttempts} attempts" : $"attempt {attempts}";
}
