using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// The same shape of token as <see cref="ExtUnverifiedEmailTakeoverProbe"/> - an unverified email
/// claim matching an existing local account, from an unrecognised subject - but run against
/// <see cref="StrictSubjectExternalUserResolver"/>, the subject-matching resolver a safety-conscious
/// host would write instead of the harness's own naive one.
/// </summary>
internal sealed class ExtStrictResolverRejectsProbe : IAuthProbe
{
    public string Id => "EXT-STRICT-RESOLVER-REJECTS";
    public string Category => "External token exchange";
    public string Attack => "Valid token, allowed tenant, email of an existing local account, email_verified: false, an unknown subject - run against the subject-matching resolver.";
    public string Expectation => "Rejected - an unrecognised subject has no local account under subject-matching resolution, regardless of what its email claims to be.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var victimEmail = CoreSessionSupport.UniqueEmail("strict-victim");
        await CoreSessionSupport.SeedUserAsync(host.Context, victimEmail);

        host.Context.Services.GetRequiredService<ExternalUserResolverSwitch>().Mode = ExternalUserResolverMode.Strict;

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, subject: "attacker-subject-never-seen-before", email: victimEmail, emailVerified: false);
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence($"Strict (subject-matching) resolver: email = '{victimEmail}' (an existing local account), subject never seen before.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. The subject-matching resolver accepted an unrecognised subject.", evidence);

        return ProbeResult.Passed("The subject-matching resolver rejected an unrecognised subject, regardless of a matching email claim.", evidence);
    }
}
