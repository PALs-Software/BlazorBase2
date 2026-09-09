using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.ClientPipeline;

/// <summary>
/// A 401 from any endpoint, through <see cref="Services.SessionExpiryHandler"/>, is meant to drop the
/// cached authenticated principal <see cref="Services.BlazorBaseUserAuthStateProvider"/> holds -
/// without this, a session revoked server-side leaves the UI showing a signed-in user next to every
/// request being refused. Composed exactly as <c>BlazorBase.User.md</c> documents it for the Bearer
/// transport (<see cref="Services.IAuthService"/>'s own <see cref="HttpClient"/> carries no
/// <see cref="Services.AuthTokenHandler"/>, "the auth endpoints are anonymous and need no bearer"),
/// the warm-up step this probe needs never succeeds in the first place - recorded as the finding, not
/// worked around.
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
            Request: "GET api/auth/session through IAuthService's own HttpClient (no AuthTokenHandler attached to it, by the framework's own composition rule).",
            Response: $"IsAuthenticated: {authenticatedAfterLogin}");

        if (!authenticatedAfterLogin)
        {
            return ProbeResult.Failed(
                "BlazorBaseUserAuthStateProvider never becomes authenticated under the Bearer transport when composed exactly as BlazorBase.User.md documents it: " +
                "GetAuthenticationStateAsync() always resolves the principal through IAuthService.GetSessionAsync(), and IAuthService's own HttpClient deliberately " +
                "carries no AuthTokenHandler - its own registration comment says the auth endpoints are anonymous and need no bearer - so api/auth/session is always " +
                "called without the access token and always answers anonymous, even immediately after a successful login. The 401-invalidation behaviour this probe " +
                "set out to test never gets a chance to matter, because the cached state it would invalidate never becomes authenticated in the first place. " +
                "The framework's own BlazorBaseUserAuthStateProviderTests mask this with a stub IAuthService that returns a canned authenticated session " +
                "regardless of transport, rather than the real GetSessionAsync() implementation.",
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
