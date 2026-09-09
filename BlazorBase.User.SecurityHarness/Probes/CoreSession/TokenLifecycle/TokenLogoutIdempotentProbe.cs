using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>Logs out twice with the same token, and once with a token that was never issued.</summary>
internal sealed class TokenLogoutIdempotentProbe : IAuthProbe
{
    public string Id => "TOKEN-LOGOUT-IDEMPOTENT";
    public string Category => "Token lifecycle";
    public string Attack => "Logs out with a live token, logs out again with the same (now-revoked) token, and logs out with a token that was never issued.";
    public string Expectation => "No error in any case, and no observable difference between them - logout is not an existence oracle.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("logout-idempotent");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        var firstLogout = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/logout",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken }, cancellationToken: cancellationToken);
        var secondLogout = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/logout",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken }, cancellationToken: cancellationToken);
        var neverIssuedLogout = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/logout",
            new RefreshTokenRequest { RefreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) }, cancellationToken: cancellationToken);

        var evidence = new[]
        {
            firstLogout.ToEvidence("First logout, with a live token."),
            secondLogout.ToEvidence("Second logout, with the same (now-revoked) token."),
            neverIssuedLogout.ToEvidence("Logout with a token that was never issued."),
        };

        var statuses = new[] { firstLogout.StatusCode, secondLogout.StatusCode, neverIssuedLogout.StatusCode };

        if (statuses.Any(status => status is >= System.Net.HttpStatusCode.InternalServerError))
            return ProbeResult.Failed("At least one logout call answered with a server error.", evidence);

        if (statuses.Distinct().Count() > 1)
            return ProbeResult.Failed($"Logout answered differently across the three cases ({string.Join(", ", statuses.Select(status => (int)status))}) - it can be used to tell whether a token existed.", evidence);

        return ProbeResult.Passed($"All three logout calls answered identically ({(int)statuses[0]}) - live, already-revoked, and never-issued tokens are indistinguishable.", evidence);
    }
}
