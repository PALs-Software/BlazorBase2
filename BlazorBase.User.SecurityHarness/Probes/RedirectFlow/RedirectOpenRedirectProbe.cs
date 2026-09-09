using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.RedirectFlow;

/// <summary>
/// One probe over a table of open-redirect and header-injection payloads for the redirect flow's
/// <c>returnUrl</c> - not eleven separate probes, so a single defence (or a single gap) is reported
/// as one finding with every payload's outcome attached as evidence.
/// </summary>
internal sealed class RedirectOpenRedirectProbe : IAuthProbe
{
    private static readonly IReadOnlyList<string> Payloads =
    [
        "//evil.example",
        "/\\evil.example",
        "https://evil.example",
        "\\\\evil.example",
        "/%2f%2fevil.example",
        "/%5c%5cevil.example",
        "%2f%2fevil.example",
        "/..//evil.example",
        "/\\/evil.example",
        "/\tevil.example",
        "/\r\nSet-Cookie: injected=1",
    ];

    public string Id => "REDIR-OPEN-REDIRECT";
    public string Category => "Redirect flow";
    public string Attack => $"Completes the redirect flow with returnUrl set to each of {Payloads.Count} open-redirect / header-injection payloads.";
    public string Expectation => "Every payload falls back to the default return URL; the final Location never points off-origin and never carries an injected header.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;
        await RedirectFlowSupport.EnsureResolvableSubjectAsync(context);

        var evidence = new List<ProbeEvidence>();
        var unsafeOutcomes = new List<string>();
        var unresolvedPayloads = new List<string>();

        foreach (var payload in Payloads)
        {
            using var client = RedirectFlowSupport.CreateClient(context);
            var chain = await RedirectFlowSupport.RunChallengeAsync(client, context.BaseAddress, payload, cancellationToken);
            evidence.Add(chain.ToEvidence($"Payload: {Describe(payload)}"));

            if (chain.Final.Exchange.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.Found))
            {
                unresolvedPayloads.Add(Describe(payload));
                continue;
            }

            var location = chain.Final.Exchange.ResponseHeaders.TryGetValues("Location", out var values) ? values.FirstOrDefault() : null;

            if (IsUnsafeLocation(location))
                unsafeOutcomes.Add($"{Describe(payload)} -> {location ?? "<none>"}");
        }

        if (unsafeOutcomes.Count > 0)
            return ProbeResult.Failed($"{unsafeOutcomes.Count} of {Payloads.Count} payloads produced an unsafe final Location: {string.Join("; ", unsafeOutcomes)}.", evidence);

        if (unresolvedPayloads.Count == Payloads.Count)
            return ProbeResult.Inconclusive(
                "The redirect flow never reached a completed redirect for any payload - every one stopped at ExternalAuthRedirectEndpointMapper.Complete's own " +
                "httpContext.AuthenticateAsync(externalScheme) call, which never succeeds against a real RemoteAuthenticationHandler (OpenIdConnect) outside its own " +
                "callback processing (see REDIR-COMPLETE-REPLAY). The open-redirect defence could not be observed for lack of a completed flow to inspect.", evidence);

        if (unresolvedPayloads.Count > 0)
            return ProbeResult.Inconclusive(
                $"{unresolvedPayloads.Count} of {Payloads.Count} payloads did not reach a completed redirect and could not be assessed; " +
                $"the remaining {Payloads.Count - unresolvedPayloads.Count} all fell back to a safe, on-origin return URL.", evidence);

        return ProbeResult.Passed($"All {Payloads.Count} open-redirect / header-injection payloads fell back to a safe, on-origin return URL.", evidence);
    }

    private static bool IsUnsafeLocation(string? location)
    {
        if (location is null)
            return false;

        if (location.Contains("evil.example", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!location.StartsWith('/'))
            return true;

        if (location.StartsWith("//", StringComparison.Ordinal) || location.StartsWith("/\\", StringComparison.Ordinal))
            return true;

        return location.Contains('\r') || location.Contains('\n');
    }

    private static string Describe(string payload) => payload.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
}
