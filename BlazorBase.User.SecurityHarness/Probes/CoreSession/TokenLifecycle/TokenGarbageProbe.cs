using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.TokenLifecycle;

/// <summary>Sends malformed, truncated and very large refresh-token and bearer-token values.</summary>
internal sealed class TokenGarbageProbe : IAuthProbe
{
    public string Id => "TOKEN-GARBAGE";
    public string Category => "Token lifecycle";
    public string Attack => "Malformed, truncated, empty-after-trim, and very large (1 MB) token values against api/auth/refresh and a protected endpoint's Authorization header.";
    public string Expectation => "Rejected cleanly - never a 500.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var garbageValues = new (string Description, string Value)[]
        {
            ("Not base64, not a real token", "%%%not-a-real-token%%%"),
            ("Truncated to a single character", "a"),
            ("Whitespace only", "   "),
            ("Very large (1 MB of 'a')", new string('a', 1024 * 1024)),
        };

        var evidence = new List<ProbeEvidence>();
        var failures = new List<string>();

        foreach (var (description, value) in garbageValues)
        {
            var refreshExchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/refresh",
                new RefreshTokenRequest { RefreshToken = value }, cancellationToken: cancellationToken);
            evidence.Add(refreshExchange.ToEvidence($"api/auth/refresh - {description}."));

            if (refreshExchange.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
                failures.Add($"api/auth/refresh returned {(int)refreshExchange.StatusCode} for '{description}'");

            await ProbeAuthorizationHeaderAsync(client, host.Context.BaseAddress, description, value, evidence, failures, cancellationToken);
        }

        if (failures.Count > 0)
            return ProbeResult.Failed($"{failures.Count} garbage value(s) produced a server error: {string.Join("; ", failures)}.", evidence);

        return ProbeResult.Passed($"All {garbageValues.Length} garbage values were rejected cleanly on both endpoints - no server error.", evidence);
    }

    /// <summary>
    /// Sends one garbage value as an <c>Authorization</c> header. Unlike the request body, an
    /// oversized header can be rejected by Kestrel before the request ever reaches the application,
    /// which is a clean rejection too - just one with a dropped connection instead of a status code.
    /// </summary>
    private static async Task ProbeAuthorizationHeaderAsync(
        HttpClient client,
        Uri baseAddress,
        string description,
        string value,
        List<ProbeEvidence> evidence,
        List<string> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            var protectedExchange = await CoreSessionSupport.CallProtectedEndpointAsync(client, baseAddress, value, cancellationToken);
            evidence.Add(protectedExchange.ToEvidence($"Protected endpoint Authorization header - {description}."));

            if (protectedExchange.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
                failures.Add($"the protected endpoint returned {(int)protectedExchange.StatusCode} for '{description}'");
        }
        catch (HttpRequestException ex)
        {
            evidence.Add(new ProbeEvidence($"Protected endpoint Authorization header - {description}.", Request: $"GET harness/protected-probe (Authorization: Bearer <{description}>)", Response: $"Connection-level rejection: {ex.Message}"));
        }
    }
}
