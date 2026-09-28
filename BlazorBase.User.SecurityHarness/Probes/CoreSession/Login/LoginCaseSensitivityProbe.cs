using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.CoreSession.Login;

/// <summary>Logs in with the seeded email's case flipped, and documents whether it still succeeds.</summary>
internal sealed class LoginCaseSensitivityProbe : IAuthProbe
{
    public string Id => "LOGIN-CASE-SENSITIVITY";
    public string Category => "Login";
    public string Attack => "Login with the email address in a different case than it was registered with.";
    public string Expectation => "Documents whether login succeeds regardless of case - not a pass/fail check, since either answer can be correct depending on the host's intent.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Info;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var email = CoreSessionSupport.UniqueEmail("case-sensitivity");
        await CoreSessionSupport.SeedUserAsync(host.Context, email);

        using var client = host.Context.HttpClientFactory();
        var flippedCaseEmail = FlipCase(email);
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/login",
            new LoginRequest { Email = flippedCaseEmail, Password = CoreSessionSupport.SeedPassword }, cancellationToken: cancellationToken);

        var evidence = exchange.ToEvidence($"Login as '{flippedCaseEmail}' for an account registered as '{email}'.");
        var succeeded = exchange.StatusCode == System.Net.HttpStatusCode.OK;

        return ProbeResult.Passed(
            succeeded
                ? "Login succeeded with the email's case flipped - email matching is case-insensitive."
                : $"Login failed ({(int)exchange.StatusCode} {exchange.StatusCode}) with the email's case flipped - email matching is case-sensitive.",
            evidence);
    }

    private static string FlipCase(string value) =>
        new([.. value.Select(character => char.IsUpper(character) ? char.ToLowerInvariant(character) : char.ToUpperInvariant(character))]);
}
