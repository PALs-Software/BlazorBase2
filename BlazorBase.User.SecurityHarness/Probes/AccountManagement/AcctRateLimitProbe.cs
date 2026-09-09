using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Floods <c>forgot-password</c> beyond a lowered permit limit. Runs on its own isolated host with
/// both the permit limit and the response-timing floor lowered - the floor is irrelevant to what this
/// probe measures and would otherwise add several real seconds per request across the flood.
/// </summary>
internal sealed class AcctRateLimitProbe : IAuthProbe
{
    private const int PermitLimit = 3;

    public string Id => "ACCT-RATE-LIMIT";
    public string Category => "Account management";
    public string Attack => $"{PermitLimit + 2} forgot-password requests inside one window, against a host configured with a {PermitLimit}-request permit limit.";
    public string Expectation => "The excess requests are rejected with 429.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["AuthTokens:RateLimit:PermitLimit"] = PermitLimit.ToString(),
            ["AuthTokens:RateLimit:WindowSeconds"] = "60",
            ["AccountManagement:TimingFloorMinimumMilliseconds"] = "0",
            ["AccountManagement:TimingFloorMaximumMilliseconds"] = "0",
        };
        await using var host = await context.StartIsolatedHostAsync(overrides, cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var evidence = new List<ProbeEvidence>();
        var statusCodes = new List<System.Net.HttpStatusCode>();

        for (var request = 1; request <= PermitLimit + 2; request++)
        {
            var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/forgot-password",
                new ForgotPasswordRequest { Email = CoreSessionSupport.UniqueEmail("rate-limit") }, cancellationToken: cancellationToken);
            statusCodes.Add(exchange.StatusCode);
            evidence.Add(exchange.ToEvidence($"Request {request} of {PermitLimit + 2}."));
        }

        var rejectedCount = statusCodes.Count(status => status == System.Net.HttpStatusCode.TooManyRequests);
        var expectedRejections = statusCodes.Count - PermitLimit;

        if (rejectedCount < expectedRejections)
            return ProbeResult.Failed($"Expected at least {expectedRejections} of {statusCodes.Count} requests to be 429; got {rejectedCount}.", evidence);

        return ProbeResult.Passed($"{rejectedCount} of {statusCodes.Count} requests were rejected with 429 once the {PermitLimit}-request permit limit was exceeded.", evidence);
    }
}
