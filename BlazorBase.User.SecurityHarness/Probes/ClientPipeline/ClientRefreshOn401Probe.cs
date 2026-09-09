using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.ClientPipeline;

/// <summary>
/// The canary for this probe group: a real client stack (<see cref="ClientPipelineSupport.BuildClientStack"/>)
/// calls a protected endpoint after its access token has expired server-side but still looks valid to
/// the client's own clock - <see cref="HarnessTimeProvider"/> is advanced on the server only, so the
/// client's proactive <c>IsAboutToExpire</c> check does not fire and the 401 must come from the wire.
/// </summary>
internal sealed class ClientRefreshOn401Probe : IAuthProbe
{
    private const int AccessTokenMinutes = 10;

    public string Id => "CLIENT-REFRESH-ON-401";
    public string Category => "Client pipeline";
    public string Attack => "An expired access token on a normal call through the real client pipeline.";
    public string Expectation => "Exactly one refresh, then one retry, then success. Canary for this group.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["AuthTokens:AccessTokenMinutes"] = AccessTokenMinutes.ToString() };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("client-refresh-401");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var stack = ClientPipelineSupport.BuildClientStack(host.Context.BaseAddress);
        var originalTokens = await ClientPipelineSupport.LoginAndAdoptAsync(stack, email, cancellationToken);

        var harnessTimeProvider = (HarnessTimeProvider)host.Context.TimeProvider;
        harnessTimeProvider.Advance(TimeSpan.FromMinutes(AccessTokenMinutes) + TimeSpan.FromMinutes(1));

        using var response = await stack.ApiHttpClient.GetAsync("harness/protected-probe", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var renewedTokens = await stack.TokenStorage.GetTokensAsync();

        var evidence = new ProbeEvidence(
            "The call through the composed client pipeline, and the refresh-endpoint call count it caused.",
            Request: "GET harness/protected-probe, through AuthTokenHandler + SessionExpiryHandler, with the server clock advanced 11 minutes past a 10-minute access-token lifetime.",
            Response: $"{(int)response.StatusCode} {response.StatusCode}\n{body}\nRefresh calls made: {stack.RefreshCounter.Count}\n" +
                      $"Original access token: {originalTokens.AccessToken}\nToken after the call: {renewedTokens?.AccessToken}");

        if (response.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed($"Canary failed: expected 200 after the client's own refresh-and-retry, got {(int)response.StatusCode}.", evidence);

        if (stack.RefreshCounter.Count != 1)
            return ProbeResult.Failed($"Canary failed: expected exactly one refresh call, got {stack.RefreshCounter.Count}.", evidence);

        if (renewedTokens is null || renewedTokens.AccessToken == originalTokens.AccessToken)
            return ProbeResult.Failed("Canary failed: the token storage was not updated with a new access token.", evidence);

        return ProbeResult.Passed("Exactly one refresh call renewed the session, and the original request succeeded after one retry.", evidence);
    }
}
