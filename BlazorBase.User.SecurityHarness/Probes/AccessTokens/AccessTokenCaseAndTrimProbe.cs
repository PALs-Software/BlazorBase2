using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>The valid secret with altered case, and with surrounding whitespace, must both be rejected.</summary>
internal sealed class AccessTokenCaseAndTrimProbe : IAuthProbe
{
    public string Id => "AT-CASE-AND-TRIM";
    public string Category => "Access tokens";
    public string Attack => "Presents the valid secret with its case flipped, and again with leading/trailing whitespace inside the raw Authorization header.";
    public string Expectation => "Both rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;

        var created = await AccessTokenSupport.CreateTokenAsync(context, "at-case-and-trim", null, null, cancellationToken);
        using var client = context.HttpClientFactory();

        var flippedCase = FlipCase(created.Secret);
        var caseExchange = await SendWithRawAuthorizationAsync(client, context.BaseAddress, $"Bearer {flippedCase}", cancellationToken);

        var trimExchange = await SendWithRawAuthorizationAsync(client, context.BaseAddress, $"Bearer  {created.Secret}  ", cancellationToken);

        var evidence = new List<ProbeEvidence>
        {
            caseExchange.ToEvidence("The valid secret with its case flipped."),
            trimExchange.ToEvidence("The valid secret with extra whitespace inside the raw Authorization header value."),
        };

        var failures = new List<string>();

        if (caseExchange.StatusCode != HttpStatusCode.Unauthorized)
            failures.Add($"case-flipped secret answered {(int)caseExchange.StatusCode} {caseExchange.StatusCode}");

        if (trimExchange.StatusCode != HttpStatusCode.Unauthorized)
            failures.Add($"whitespace-padded secret answered {(int)trimExchange.StatusCode} {trimExchange.StatusCode}");

        if (failures.Count > 0)
            return ProbeResult.Failed($"Expected both variants rejected with 401: {string.Join("; ", failures)}.", evidence);

        return ProbeResult.Passed("Both a case-flipped and a whitespace-padded variant of the valid secret were rejected.", evidence);
    }

    private static Task<ProbeExchange> SendWithRawAuthorizationAsync(HttpClient client, Uri baseAddress, string rawAuthorizationHeader, CancellationToken cancellationToken) =>
        CoreSessionSupport.SendAsync(client, HttpMethod.Get, baseAddress, "harness/access-token-probe",
            headers: [new KeyValuePair<string, string>("Authorization", rawAuthorizationHeader)], cancellationToken: cancellationToken);

    private static string FlipCase(string secret) => new([.. secret.Select(character =>
        char.IsLetter(character) ? (char.IsUpper(character) ? char.ToLowerInvariant(character) : char.ToUpperInvariant(character)) : character)]);
}
