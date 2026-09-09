using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Presents user A's email-confirmation token together with user B's id. Confirmation tokens embed
/// the user id they were minted for, so a token minted for A should never validate against B.
/// </summary>
internal sealed class AcctConfirmCrossUserProbe : IAuthProbe
{
    public string Id => "ACCT-CONFIRM-CROSS-USER";
    public string Category => "Account management";
    public string Attack => "Presents user A's email-confirmation token with user B's id.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        var emailA = CoreSessionSupport.UniqueEmail("confirm-cross-a");
        var emailB = CoreSessionSupport.UniqueEmail("confirm-cross-b");
        var userA = await CoreSessionSupport.SeedUserAsync(host.Context, emailA);
        var userB = await CoreSessionSupport.SeedUserAsync(host.Context, emailB);
        var tokenForA = await AccountManagementSupport.GenerateEmailConfirmationTokenAsync(host.Context, userA);

        using var client = host.Context.HttpClientFactory();
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, host.Context.BaseAddress, "api/auth/manage/confirm-email",
            new ConfirmEmailRequest { UserId = userB.Id, Token = tokenForA }, cancellationToken: cancellationToken);
        var evidence = exchange.ToEvidence("User A's confirmation token, presented with user B's id.");

        if (exchange.StatusCode == HttpStatusCode.OK)
            return ProbeResult.Failed("User A's confirmation token was accepted against user B's account.", evidence);

        return ProbeResult.Passed("User A's confirmation token was rejected when presented with user B's id.", evidence);
    }
}
