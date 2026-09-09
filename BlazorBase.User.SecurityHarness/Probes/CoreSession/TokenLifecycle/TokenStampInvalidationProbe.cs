using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>
/// Rotates the security stamp of a signed-in user, then measures how long the access token minted
/// before that rotation keeps authenticating - a live access token is never re-checked against the
/// database, so this is a measurement of the framework's design (the access-token lifetime is the
/// actual bound on a stamp-based revocation), not a threshold check.
/// </summary>
internal sealed class TokenStampInvalidationProbe : IAuthProbe
{
    private const int AccessTokenMinutes = 1;

    public string Id => "TOKEN-STAMP-INVALIDATION";
    public string Category => "Token lifecycle";
    public string Attack => "Rotates the user's security stamp, then uses an access token minted before that rotation.";
    public string Expectation => "Measures how long the old access token keeps working after the stamp was rotated, and reports the number.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?> { ["AuthTokens:AccessTokenMinutes"] = AccessTokenMinutes.ToString() };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("stamp-invalidation");
        var user = await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        await CoreSessionSupport.RotateSecurityStampAsync(host.Context, user.Id);

        var immediatelyAfterRotation = await CoreSessionSupport.CallProtectedEndpointAsync(client, host.Context.BaseAddress, tokens.AccessToken, cancellationToken);
        var evidenceImmediate = immediatelyAfterRotation.ToEvidence("Protected endpoint, with the pre-rotation access token, immediately after the security stamp was rotated.");
        var stillValidImmediatelyAfterRotation = immediatelyAfterRotation.StatusCode == System.Net.HttpStatusCode.OK;

        var harnessTimeProvider = (HarnessTimeProvider)host.Context.TimeProvider;
        harnessTimeProvider.Advance(TimeSpan.FromMinutes(AccessTokenMinutes) + TimeSpan.FromSeconds(5));

        var afterNaturalExpiry = await CoreSessionSupport.CallProtectedEndpointAsync(client, host.Context.BaseAddress, tokens.AccessToken, cancellationToken);
        var evidenceAfterExpiry = afterNaturalExpiry.ToEvidence($"The same pre-rotation access token, after advancing the harness TimeProvider by its full {AccessTokenMinutes}-minute lifetime plus 5 seconds.");
        var invalidAfterNaturalExpiry = afterNaturalExpiry.StatusCode == System.Net.HttpStatusCode.Unauthorized;

        var summary = stillValidImmediatelyAfterRotation
            ? $"The pre-rotation access token kept authenticating immediately after the stamp rotation, and only stopped working {(invalidAfterNaturalExpiry ? "at its natural expiry" : "at an undetermined later point")} ({AccessTokenMinutes} minute(s) later) - stamp rotation added 0 seconds of protection to a live access token."
            : "The pre-rotation access token stopped authenticating immediately after the stamp rotation - the access token IS re-checked against the security stamp.";

        return ProbeResult.Inconclusive(summary, evidenceImmediate, evidenceAfterExpiry);
    }
}
