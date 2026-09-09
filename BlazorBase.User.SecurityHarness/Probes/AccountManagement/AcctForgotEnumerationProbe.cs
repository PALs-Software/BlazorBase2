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
    /// <summary>
    /// Higher than the other timing probes' 30 (<c>LOGIN-TIMING-ORACLE</c>, <c>AT-PREFIX-TIMING</c>)
    /// because <c>forgot-password</c>'s own randomised 750-1250 ms timing floor is, by design, a far
    /// wider noise source (roughly 150-270 ms of interquartile range per group) than the raw
    /// processing-time noise those probes measure - a bootstrap confidence interval on the median
    /// needs more samples to reach the same coverage against that much larger, self-inflicted noise.
    /// </summary>
    private const int SampleCount = 40;

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

        var (known, unknown) = await AccountManagementSupport.SampleEndpointPairAsync(client, host.Context.BaseAddress, "api/auth/manage/forgot-password",
            () => new ForgotPasswordRequest { Email = knownEmail }, () => new ForgotPasswordRequest { Email = unknownEmail }, SampleCount, cancellationToken);

        return AccountEnumerationVerdict.Evaluate("forgot-password", known, unknown);
    }
}
