using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Samples <c>forgot-password</c> for a real, seeded address and for one that does not exist, since
/// both branches already answer with an identical <c>202</c> and an empty body -
/// <c>AccountEndpointMapper.ForgotPasswordAsync</c>'s randomised timing floor exists specifically to
/// close the remaining gap: the existing-address branch performs extra work (token generation) the
/// unknown-address branch does not.
/// </summary>
internal sealed class AcctForgotEnumerationProbe : IAuthProbe
{
    private const int SampleCount = 15;

    private static readonly Dictionary<string, string?> RateLimitOverrides =
        new() { ["AuthTokens:RateLimit:PermitLimit"] = "1000" };

    public string Id => "ACCT-FORGOT-ENUMERATION";
    public string Category => "Account management";
    public string Attack => $"{SampleCount} timed forgot-password requests for a real, seeded address, and {SampleCount} for an address that does not exist.";
    public string Expectation => "Identical status, identical body, and overlapping response-time distributions - no account-enumeration oracle either way.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(RateLimitOverrides, cancellationToken);
        var knownEmail = CoreSessionSupport.UniqueEmail("forgot-known");
        await CoreSessionSupport.SeedUserAsync(host.Context, knownEmail);
        var unknownEmail = CoreSessionSupport.UniqueEmail("forgot-unknown");

        using var client = host.Context.HttpClientFactory();

        var known = await AccountManagementSupport.SampleEndpointAsync(client, host.Context.BaseAddress, "api/auth/manage/forgot-password",
            () => new ForgotPasswordRequest { Email = knownEmail }, SampleCount, cancellationToken);
        var unknown = await AccountManagementSupport.SampleEndpointAsync(client, host.Context.BaseAddress, "api/auth/manage/forgot-password",
            () => new ForgotPasswordRequest { Email = unknownEmail }, SampleCount, cancellationToken);

        return AccountEnumerationVerdict.Evaluate("forgot-password", known, unknown);
    }
}
