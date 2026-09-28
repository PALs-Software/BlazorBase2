using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.SecurityHarness.Probes.Disclosure;

/// <summary>
/// Logs in and reads back the claim list <c>api/auth/session</c> reports for that same caller,
/// checking specifically for <see cref="ClaimsIdentityOptions.SecurityStampClaimType"/> (read from
/// the host's own <see cref="IdentityOptions"/> rather than hardcoded) - <c>SignInManager.CreateUserPrincipalAsync</c>
/// adds that claim to the principal that both mints the access token and is echoed back by the
/// session endpoint, so whatever claims are in the token are exactly what api/auth/session reveals.
/// </summary>
internal sealed class InfoSessionClaimsProbe : IAuthProbe
{
    public string Id => "INFO-SESSION-CLAIMS";
    public string Category => "Disclosure";
    public string Attack => "Logs in, then reads the claim list api/auth/session reports for that caller.";
    public string Expectation => "Contains no security stamp, no password hash, no internal identifier not needed by the client.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("session-claims");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var (tokens, loginExchange) = await CoreSessionSupport.LoginAsync(client, host.Context.BaseAddress, email, CoreSessionSupport.SeedPassword, cancellationToken);

        if (tokens is null)
            return ProbeResult.Inconclusive("Login did not return a token pair.", loginExchange.ToEvidence("Login."));

        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, host.Context.BaseAddress, "api/auth/session", bearerToken: tokens.AccessToken, cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("Authenticated session call.");

        if (!CoreSessionSupport.TryDeserialize<AuthSessionResponse>(exchange.ResponseBody, out var session) || session is null)
            return ProbeResult.Failed("The session response could not be parsed.", evidence);

        var securityStampClaimType = host.Context.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType;
        var claimTypes = session.Claims.Select(claim => claim.Type).ToList();
        var securityStampClaim = session.Claims.FirstOrDefault(claim => claim.Type == securityStampClaimType);

        var summary = $"Claims reported for the authenticated caller: [{string.Join(", ", claimTypes)}].";

        if (securityStampClaim is not null)
            return ProbeResult.Failed($"{summary} The security stamp claim ('{securityStampClaimType}') is present, value '{securityStampClaim.Value}' - SignInManager.CreateUserPrincipalAsync adds it to the same principal that mints the access token, so it is embedded in the token and echoed back here.", evidence);

        return ProbeResult.Passed($"{summary} No security stamp claim among them.", evidence);
    }
}
