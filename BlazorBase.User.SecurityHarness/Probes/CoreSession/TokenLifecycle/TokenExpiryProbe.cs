using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>Advances the harness clock past the access-token lifetime and calls a protected endpoint.</summary>
internal sealed class TokenExpiryProbe : IAuthProbe
{
    private const int AccessTokenMinutes = 1;

    public string Id => "TOKEN-EXPIRY";
    public string Category => "Token lifecycle";
    public string Attack => $"Advances the harness TimeProvider past the access-token lifetime ({AccessTokenMinutes} minute).";
    public string Expectation => "401 on a protected endpoint.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["AuthTokens:AccessTokenMinutes"] = AccessTokenMinutes.ToString() };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("expiry");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair; expiry cannot be measured.", loginExchange.ToEvidence("Login."));

        var beforeExpiry = await CoreSessionSupport.CallProtectedEndpointAsync(client, host.Context.BaseAddress, tokens.AccessToken, cancellationToken);
        var evidenceBefore = beforeExpiry.ToEvidence("Protected endpoint with a fresh access token, before any time has passed.");

        if (beforeExpiry.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Inconclusive($"A fresh access token did not authenticate ({(int)beforeExpiry.StatusCode}) - expiry cannot be measured against a token that never worked.", evidenceBefore);

        var harnessTimeProvider = (HarnessTimeProvider)host.Context.TimeProvider;
        harnessTimeProvider.Advance(TimeSpan.FromMinutes(AccessTokenMinutes) + TimeSpan.FromSeconds(5));

        var afterExpiry = await CoreSessionSupport.CallProtectedEndpointAsync(client, host.Context.BaseAddress, tokens.AccessToken, cancellationToken);
        var evidenceAfter = afterExpiry.ToEvidence($"The same access token after advancing the harness TimeProvider by {AccessTokenMinutes} minute(s) plus 5 seconds - no real wait.");

        if (afterExpiry.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401 after advancing the clock past expiry, got {(int)afterExpiry.StatusCode} {afterExpiry.StatusCode}. Either the bearer scheme does not honour the DI-registered TimeProvider, or the token never actually expired.", evidenceBefore, evidenceAfter);

        return ProbeResult.Passed("The access token stopped authenticating once the harness TimeProvider was advanced past its lifetime, with no real wait - the bearer scheme honours the DI-registered TimeProvider.", evidenceBefore, evidenceAfter);
    }
}
