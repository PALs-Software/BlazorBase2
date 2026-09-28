using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// A correctly signed token whose <c>exp</c> is well in the past - far outside the validator's
/// 30-second clock skew, unlike <see cref="ExtExpiredWithinSkewProbe"/>.
/// </summary>
internal sealed class ExtExpiredProbe : IAuthProbe
{
    public string Id => "EXT-EXPIRED";
    public string Category => "External token exchange";
    public string Attack => "Correctly signed, but exp is 10 minutes in the past - far outside the 30-second clock skew.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var now = DateTimeOffset.UtcNow;
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, issuedAt: now.AddMinutes(-20), notBefore: now.AddMinutes(-20), expiresAt: now.AddMinutes(-10));
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence("exp 10 minutes in the past.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. A token expired well outside the clock skew was not rejected.", evidence);

        return ProbeResult.Passed("A token expired well outside the clock skew was rejected.", evidence);
    }
}
