using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.Disclosure;

/// <summary>
/// Sends malformed bodies, a wrong content type, and an unsupported method against the auth routes,
/// and checks every response body for anything that looks like a stack trace, an exception type name,
/// or a framework/runtime identifier - none of the malformed-input handling here is expected to throw
/// an unhandled exception, so this looks for evidence that it does rather than assuming it cannot.
/// </summary>
internal sealed class InfoErrorBodiesProbe : IAuthProbe
{
    private static readonly string[] SuspiciousPatterns =
    [
        "Exception",
        "StackTrace",
        ".cs:line",
        " at System.",
        " at Microsoft.",
        " at BlazorBase.",
        "innerException",
    ];

    public string Id => "INFO-ERROR-BODIES";
    public string Category => "Disclosure";
    public string Attack => "Malformed JSON, a wrong content type, and an unsupported HTTP method against several auth routes.";
    public string Expectation => "No stack trace, no framework version, no exception type in any response body.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var evidence = new List<ProbeEvidence>();
        var suspiciousFindings = new List<string>();

        await ProbeAsync(client, host.Context.BaseAddress, HttpMethod.Post, "api/auth/login", "not-json-at-all", "application/json", evidence, suspiciousFindings, cancellationToken);
        await ProbeAsync(client, host.Context.BaseAddress, HttpMethod.Post, "api/auth/login", "{\"email\":\"a@b.com\",\"password\":\"x\"}", "text/plain", evidence, suspiciousFindings, cancellationToken);
        await ProbeAsync(client, host.Context.BaseAddress, HttpMethod.Delete, "api/auth/login", null, null, evidence, suspiciousFindings, cancellationToken);
        await ProbeAsync(client, host.Context.BaseAddress, HttpMethod.Post, "api/auth/refresh", "{ malformed", "application/json", evidence, suspiciousFindings, cancellationToken);
        await ProbeAsync(client, host.Context.BaseAddress, HttpMethod.Get, "api/auth/manage/reset-password", null, null, evidence, suspiciousFindings, cancellationToken);

        if (suspiciousFindings.Count > 0)
            return ProbeResult.Failed($"{suspiciousFindings.Count} response(s) contained a suspicious pattern: {string.Join("; ", suspiciousFindings)}.", evidence);

        return ProbeResult.Passed("No response body carried a stack trace, exception type, or framework identifier.", evidence);
    }

    private static async Task ProbeAsync(
        HttpClient client,
        Uri baseAddress,
        HttpMethod method,
        string path,
        string? rawBody,
        string? contentType,
        List<ProbeEvidence> evidence,
        List<string> suspiciousFindings,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(baseAddress, path));

        if (rawBody is not null)
            request.Content = new StringContent(rawBody, System.Text.Encoding.UTF8, contentType ?? "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var description = $"{method} {path}{(contentType is not null ? $" ({contentType})" : string.Empty)}";

        evidence.Add(new ProbeEvidence(description, Request: $"{description}\n{rawBody}", Response: $"{(int)response.StatusCode} {response.StatusCode}\n{body}"));

        var matchedPattern = SuspiciousPatterns.FirstOrDefault(pattern => body.Contains(pattern, StringComparison.Ordinal));

        if (matchedPattern is not null)
            suspiciousFindings.Add($"{description} contained '{matchedPattern}'");
    }
}
