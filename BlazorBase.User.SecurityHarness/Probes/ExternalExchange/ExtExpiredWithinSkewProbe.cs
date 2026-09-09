using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// A measurement, not a verdict: how long past its own <c>exp</c> a token stays usable, against the
/// validator's documented 30-second clock skew. Tests one offset comfortably inside the skew and one
/// comfortably outside it, and reports both outcomes as the measurement.
/// </summary>
internal sealed class ExtExpiredWithinSkewProbe : IAuthProbe
{
    public string Id => "EXT-EXPIRED-WITHIN-SKEW";
    public string Category => "External token exchange";
    public string Attack => "exp 25 seconds in the past (inside the documented 30-second clock skew), and separately 35 seconds in the past (outside it).";
    public string Expectation => "A measurement, not a verdict: how long past exp a token stays usable.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Info;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);

        var insideSkewExchange = await ExchangeExpiredByAsync(client, host.Context.BaseAddress, providerHost, TimeSpan.FromSeconds(25), cancellationToken);
        var outsideSkewExchange = await ExchangeExpiredByAsync(client, host.Context.BaseAddress, providerHost, TimeSpan.FromSeconds(35), cancellationToken);

        var evidence = new[]
        {
            ExternalExchangeSupport.BuildEvidence("exp 25 seconds in the past.", insideSkewExchange.Token, insideSkewExchange.Exchange),
            ExternalExchangeSupport.BuildEvidence("exp 35 seconds in the past.", outsideSkewExchange.Token, outsideSkewExchange.Exchange),
        };

        var insideAccepted = insideSkewExchange.Exchange.StatusCode == System.Net.HttpStatusCode.OK;
        var outsideAccepted = outsideSkewExchange.Exchange.StatusCode == System.Net.HttpStatusCode.OK;

        return ProbeResult.Inconclusive(
            $"Measurement: a token expired 25 seconds ago was {(insideAccepted ? "still accepted" : "rejected")} ({(int)insideSkewExchange.Exchange.StatusCode}); a token expired 35 seconds ago was {(outsideAccepted ? "still accepted" : "rejected")} ({(int)outsideSkewExchange.Exchange.StatusCode}).",
            evidence);
    }

    private static async Task<(string Token, ProbeExchange Exchange)> ExchangeExpiredByAsync(HttpClient client, Uri baseAddress, FakeIdentityProviderHost providerHost, TimeSpan expiredBy, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, issuedAt: now.AddMinutes(-1), notBefore: now.AddMinutes(-1), expiresAt: now - expiredBy);
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);
        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, baseAddress, token, cancellationToken);

        return (token, exchange);
    }
}
