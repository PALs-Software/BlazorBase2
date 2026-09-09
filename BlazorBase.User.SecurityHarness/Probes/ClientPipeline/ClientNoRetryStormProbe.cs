using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.ClientPipeline;

/// <summary>
/// Expires the access token and revokes the refresh-token family directly, so the client's one
/// refresh attempt is guaranteed to fail server-side - then checks that <see cref="Services.AuthTokenHandler"/>
/// gives up after exactly that one attempt instead of retrying in a loop.
/// </summary>
internal sealed class ClientNoRetryStormProbe : IAuthProbe
{
    private const int AccessTokenMinutes = 10;

    public string Id => "CLIENT-NO-RETRY-STORM";
    public string Category => "Client pipeline";
    public string Attack => "A refresh that is guaranteed to fail (the refresh-token family was revoked directly before the call).";
    public string Expectation => "One refresh attempt, then sign-out - no retry loop.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["AuthTokens:AccessTokenMinutes"] = AccessTokenMinutes.ToString() };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("client-no-retry-storm");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var stack = ClientPipelineSupport.BuildClientStack(host.Context.BaseAddress);
        var originalTokens = await ClientPipelineSupport.LoginAndAdoptAsync(stack, email, cancellationToken);

        await ClientPipelineSupport.RevokeRefreshTokenFamilyAsync(host.Context, originalTokens.RefreshToken);

        var harnessTimeProvider = (HarnessTimeProvider)host.Context.TimeProvider;
        harnessTimeProvider.Advance(TimeSpan.FromMinutes(AccessTokenMinutes) + TimeSpan.FromMinutes(1));

        using var response = await stack.ApiHttpClient.GetAsync("harness/protected-probe", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokensAfter = await stack.TokenStorage.GetTokensAsync();

        var evidence = new ProbeEvidence(
            "The call through the composed client pipeline, with the refresh token's family already revoked.",
            Request: "GET harness/protected-probe, refresh-token family revoked directly, access token expired.",
            Response: $"{(int)response.StatusCode} {response.StatusCode}\n{body}\nRefresh calls made: {stack.RefreshCounter.Count}\nToken storage after the call: {(tokensAfter is null ? "cleared" : "still holds a token pair")}");

        if (stack.RefreshCounter.Count != 1)
            return ProbeResult.Failed($"Expected exactly one refresh attempt, got {stack.RefreshCounter.Count} - a retry loop.", evidence);

        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected the original 401 to be returned once the refresh failed, got {(int)response.StatusCode}.", evidence);

        if (tokensAfter is not null)
            return ProbeResult.Failed("The token storage was not cleared after the refresh failed with a rejected session.", evidence);

        return ProbeResult.Passed("Exactly one refresh attempt was made, it failed, the client signed itself out, and no retry loop occurred.", evidence);
    }
}
