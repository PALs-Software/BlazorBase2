using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.ClientPipeline;

/// <summary>
/// A 401 from any endpoint, through <see cref="Services.SessionExpiryHandler"/>, is meant to drop the
/// cached authenticated principal <see cref="Services.BlazorBaseUserAuthStateProvider"/> holds -
/// without this, a session revoked server-side leaves the UI showing a signed-in user next to every
/// request being refused. Composed exactly as <c>BlazorBase.User.md</c> documents it for the Bearer
/// transport, where <see cref="Services.IAuthService"/>'s own <see cref="HttpClient"/> carries no
/// <see cref="Services.AuthTokenHandler"/> and the provider therefore hands the access token to
/// <see cref="Services.IAuthService.GetSessionAsync"/> itself. The warm-up assertion below is part of
/// the probe rather than setup: if the principal never becomes authenticated, the invalidation this
/// probe measures can never matter.
/// </summary>
internal sealed class Client401InvalidatesProbe : IAuthProbe
{
    public string Id => "CLIENT-401-INVALIDATES";
    public string Category => "Client pipeline";
    public string Attack => "A 401 from a protected endpoint after logging in and reading back the cached authentication state.";
    public string Expectation => "The cached principal is dropped - the next authentication-state read reports anonymous.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("client-401-invalidates");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var stack = ClientPipelineSupport.BuildClientStack(host.Context.BaseAddress);
        await ClientPipelineSupport.LoginAndAdoptAsync(stack, email, cancellationToken);

        var stateAfterLogin = await stack.AuthStateProvider.GetAuthenticationStateAsync();
        var authenticatedAfterLogin = stateAfterLogin.User.Identity?.IsAuthenticated ?? false;

        var evidenceAfterLogin = new ProbeEvidence(
            "Authentication state immediately after a successful login, resolved the same way GetAuthenticationStateAsync always resolves it: through IAuthService.GetSessionAsync().",
            Request: "GET api/auth/session through IAuthService's own HttpClient, carrying the access token the provider read from ITokenStorage and passed in explicitly.",
            Response: $"IsAuthenticated: {authenticatedAfterLogin}");

        if (!authenticatedAfterLogin)
        {
            return ProbeResult.Failed(
                "BlazorBaseUserAuthStateProvider did not become authenticated under the Bearer transport when composed exactly as BlazorBase.User.md documents it. " +
                "GetAuthenticationStateAsync() resolves the principal through IAuthService.GetSessionAsync(), and since IAuthService's own HttpClient deliberately " +
                "carries no AuthTokenHandler - that handler resolves IAuthService to refresh, so attaching it would close the cycle - the provider must read the " +
                "access token from ITokenStorage and pass it to that call itself. If it does not, api/auth/session is called unauthenticated and always answers " +
                "anonymous, even immediately after a successful login, and the 401-invalidation behaviour this probe set out to test never gets a chance to matter. " +
                "The framework's own BlazorBaseUserAuthStateProviderTests cannot catch this on their own: they stub IAuthService and never exercise the real " +
                "GetSessionAsync() over HTTP.",
                evidenceAfterLogin);
        }

        await stack.TokenStorage.SaveTokensAsync(new LoginResponse
        {
            AccessToken = "garbage-access-token",
            RefreshToken = string.Empty,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        });

        using var response = await stack.ApiHttpClient.GetAsync("harness/protected-probe", cancellationToken);
        var stateAfter = await stack.AuthStateProvider.GetAuthenticationStateAsync();
        var authenticatedAfter = stateAfter.User.Identity?.IsAuthenticated ?? false;

        var evidence = new ProbeEvidence(
            "The call that produced the 401, and authentication state after.",
            Request: "GET harness/protected-probe with a garbage access token and no refresh token (so AuthTokenHandler cannot recover it).",
            Response: $"{(int)response.StatusCode} {response.StatusCode}\nIsAuthenticated after: {authenticatedAfter}");

        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Inconclusive($"The call did not answer 401 (got {(int)response.StatusCode}); invalidation cannot be measured.", evidence);

        if (authenticatedAfter)
            return ProbeResult.Failed("The cached principal still reports authenticated after a 401 from the API.", evidence);

        return ProbeResult.Passed("The cached principal was dropped to anonymous after a 401 from the API.", evidence);
    }
}
