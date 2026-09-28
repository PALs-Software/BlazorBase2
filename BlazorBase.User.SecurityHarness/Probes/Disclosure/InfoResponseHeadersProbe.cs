using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.Disclosure;

/// <summary>
/// Checks every header on a harmless anonymous response for a diagnostic or version-identifying
/// name. Kestrel's own default (<c>KestrelServerOptions.AddServerHeader</c>, true unless a host turns
/// it off) is exactly the kind of default this looks for without assuming it is already off.
/// </summary>
internal sealed class InfoResponseHeadersProbe : IAuthProbe
{
    private static readonly string[] DiagnosticHeaderNames =
    [
        "Server",
        "X-Powered-By",
        "X-AspNet-Version",
        "X-AspNetMvc-Version",
        "X-SourceFiles",
    ];

    public string Id => "INFO-RESPONSE-HEADERS";
    public string Category => "Disclosure";
    public string Attack => "Inspects every header on a harmless anonymous auth response.";
    public string Expectation => "No Server, no X-Powered-By, no diagnostic header.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Low;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, host.Context.BaseAddress, "api/auth/status", cancellationToken: cancellationToken);
        var headerNames = exchange.ResponseHeaders.Select(header => header.Key).ToList();
        var evidence = new ProbeEvidence(
            "Every response header name on GET api/auth/status.",
            Request: "GET api/auth/status",
            Response: $"{(int)exchange.StatusCode} {exchange.StatusCode}\nHeaders: [{string.Join(", ", headerNames)}]");

        var present = DiagnosticHeaderNames.Where(name => exchange.ResponseHeaders.Contains(name)).ToList();

        if (present.Count > 0)
            return ProbeResult.Failed($"Diagnostic header(s) present: {string.Join(", ", present.Select(name => $"{name}: {string.Join(",", exchange.ResponseHeaders.GetValues(name))}"))}.", evidence);

        return ProbeResult.Passed("None of the checked diagnostic headers were present.", evidence);
    }
}
