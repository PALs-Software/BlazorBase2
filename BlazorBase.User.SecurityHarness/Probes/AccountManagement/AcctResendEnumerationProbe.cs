using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// The same measurement as <see cref="AcctForgotEnumerationProbe"/>, against
/// <c>resend-confirmation</c> - a real, seeded, still-unconfirmed address against one that does not
/// exist.
/// </summary>
internal sealed class AcctResendEnumerationProbe : IAuthProbe
{
    private const int SampleCount = 15;

    private static readonly Dictionary<string, string?> RateLimitOverrides =
        new() { ["AuthTokens:RateLimit:PermitLimit"] = "1000" };

    public string Id => "ACCT-RESEND-ENUMERATION";
    public string Category => "Account management";
    public string Attack => $"{SampleCount} timed resend-confirmation requests for a real, unconfirmed address, and {SampleCount} for an address that does not exist.";
    public string Expectation => "Identical status, identical body, and overlapping response-time distributions - no account-enumeration oracle either way.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(RateLimitOverrides, cancellationToken);
        var knownEmail = CoreSessionSupport.UniqueEmail("resend-known");
        await CoreSessionSupport.SeedUserAsync(host.Context, knownEmail);
        var unknownEmail = CoreSessionSupport.UniqueEmail("resend-unknown");

        using var client = host.Context.HttpClientFactory();

        var known = await AccountManagementSupport.SampleEndpointAsync(client, host.Context.BaseAddress, "api/auth/manage/resend-confirmation",
            () => new ResendConfirmationRequest { Email = knownEmail }, SampleCount, cancellationToken);
        var unknown = await AccountManagementSupport.SampleEndpointAsync(client, host.Context.BaseAddress, "api/auth/manage/resend-confirmation",
            () => new ResendConfirmationRequest { Email = unknownEmail }, SampleCount, cancellationToken);

        return AccountEnumerationVerdict.Evaluate("resend-confirmation", known, unknown);
    }
}
