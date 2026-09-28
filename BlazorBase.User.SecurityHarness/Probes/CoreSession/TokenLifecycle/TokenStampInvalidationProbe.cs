using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>
/// Rotates the security stamp of a signed-in user, then checks whether the access token minted
/// before that rotation still authenticates on its very next use - step 10.8 re-validates a bearer
/// access token's stamp on every authenticated request by default, so a bare rotation must invalidate
/// it immediately rather than leaving the access-token lifetime as the only real bound.
/// </summary>
internal sealed class TokenStampInvalidationProbe : IAuthProbe
{
    private const int AccessTokenMinutes = 1;

    public string Id => "TOKEN-STAMP-INVALIDATION";
    public string Category => "Token lifecycle";
    public string Attack => "Rotates the user's security stamp, then uses an access token minted before that rotation.";
    public string Expectation => "The pre-rotation access token is rejected on its very next use.";
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

        var summary = stillValidImmediatelyAfterRotation
            ? $"The pre-rotation access token kept authenticating immediately after the stamp rotation ({(int)immediatelyAfterRotation.StatusCode}) - stamp rotation added 0 seconds of protection to a live access token."
            : $"The pre-rotation access token was rejected ({(int)immediatelyAfterRotation.StatusCode}) on its very next use after the stamp rotation.";

        if (stillValidImmediatelyAfterRotation)
            return ProbeResult.Failed(summary, evidenceImmediate);

        return ProbeResult.Passed(summary, evidenceImmediate);
    }
}

