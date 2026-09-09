using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.Disclosure;

/// <summary>
/// Calls <c>api/auth/session</c> with no credentials against the <c>Cookie</c> profile, where the
/// endpoint also hands out an antiforgery token to every caller, authenticated or not.
/// </summary>
internal sealed class InfoSessionAnonymousProbe : IAuthProbe
{
    public string Id => "INFO-SESSION-ANONYMOUS";
    public string Category => "Disclosure";
    public string Attack => "GET api/auth/session with no credentials.";
    public string Expectation => "Answers, but hands out nothing an anonymous caller should not have - no claims, IsAuthenticated false.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Cookie;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, host.Context.BaseAddress, "api/auth/session", cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("Anonymous session call.");

        if (!CoreSessionSupport.TryDeserialize<AuthSessionResponse>(exchange.ResponseBody, out var session) || session is null)
            return ProbeResult.Failed("The session response could not be parsed.", evidence);

        if (session.IsAuthenticated)
            return ProbeResult.Failed("An anonymous caller was reported as authenticated.", evidence);

        if (session.Claims.Count > 0)
            return ProbeResult.Failed($"An anonymous caller was handed {session.Claims.Count} claim(s): {string.Join(", ", session.Claims.Select(claim => claim.Type))}.", evidence);

        return ProbeResult.Passed(
            $"An anonymous caller got IsAuthenticated=false, no claims, and {(string.IsNullOrEmpty(session.CsrfToken) ? "no" : "an")} antiforgery token - handing out an anonymous antiforgery token is by design and does not survive the anonymous-to-authenticated transition (see CSRF-ANONYMOUS-TOKEN-REUSE).",
            evidence);
    }
}
