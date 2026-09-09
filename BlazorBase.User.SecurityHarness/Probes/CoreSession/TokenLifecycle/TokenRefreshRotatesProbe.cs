using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>Refreshes a valid session once.</summary>
internal sealed class TokenRefreshRotatesProbe : IAuthProbe
{
    public string Id => "TOKEN-REFRESH-ROTATES";
    public string Category => "Token lifecycle";
    public string Attack => "Refreshes once with a valid refresh token.";
    public string Expectation => "A new access/refresh token pair is issued and differs from the old one.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("refresh-rotates");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        var refreshExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken }, cancellationToken: cancellationToken);
        var evidence = refreshExchange.ToEvidence("Refresh with the freshly issued refresh token.");

        if (refreshExchange.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Failed($"Expected 200, got {(int)refreshExchange.StatusCode} {refreshExchange.StatusCode}.", evidence);

        if (!CoreSessionSupport.TryDeserialize<LoginResponse>(refreshExchange.ResponseBody, out var rotated) || rotated is null)
            return ProbeResult.Failed("200 response did not carry a token pair.", evidence);

        if (rotated.AccessToken == tokens.AccessToken || rotated.RefreshToken == tokens.RefreshToken)
            return ProbeResult.Failed("Rotated pair is identical to the original - refresh minted no new tokens.", evidence);

        return ProbeResult.Passed("Refresh issued a new access token and a new refresh token, both different from the originals.", evidence);
    }
}
