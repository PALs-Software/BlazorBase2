using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// Demonstrates the consequence of a host choosing the harness's own <see cref="NaiveEmailMatchingExternalUserResolver"/>:
/// an external token with an unverified, attacker-controlled <c>email</c> claim matching an existing
/// local account, from a subject the host has never seen before. This is a property of that resolver
/// - a decision the framework leaves entirely to the host, with no safe default - not a framework
/// vulnerability in itself. See <see cref="ExtStrictResolverRejectsProbe"/> for the same token against
/// the resolver a safety-conscious host would write instead.
/// </summary>
internal sealed class ExtUnverifiedEmailTakeoverProbe : IAuthProbe
{
    public string Id => "EXT-UNVERIFIED-EMAIL-TAKEOVER";
    public string Category => "External token exchange";
    public string Attack => "Valid token, allowed tenant, email of an existing local account, email_verified: false, an unknown subject - run against the naive email-matching resolver.";
    public string Expectation => "Demonstrates whether the local account is taken over. This is the consequence of a host's plausible wrong choice of account-linking resolver, not a framework vulnerability in itself - the framework ships no default resolver at all.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();

        var victimEmail = CoreSessionSupport.UniqueEmail("takeover-victim");
        await CoreSessionSupport.SeedUserAsync(host.Context, victimEmail);

        host.Context.Services.GetRequiredService<ExternalUserResolverSwitch>().Mode = ExternalUserResolverMode.Naive;

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, subject: "attacker-subject-never-seen-before", email: victimEmail, emailVerified: false);
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = new List<ProbeEvidence> { ExternalExchangeSupport.BuildEvidence($"Naive resolver: email = '{victimEmail}' (an existing local account), email_verified: false, subject never seen before.", token, exchange) };

        if (exchange.StatusCode != System.Net.HttpStatusCode.OK)
            return ProbeResult.Passed("The naive resolver did not accept an unverified email match - no takeover demonstrated with this configuration.", evidence);

        if (!CoreSessionSupport.TryDeserialize<LoginResponse>(exchange.ResponseBody, out var tokens) || tokens is null)
            return ProbeResult.Inconclusive("Got 200 but no usable access token - the takeover cannot be confirmed against the victim's identity.", evidence);

        var whoAmI = await ExternalExchangeSupport.WhoAmIAsync(client, host.Context.BaseAddress, tokens.AccessToken, cancellationToken);
        evidence.Add(whoAmI.ToEvidence("harness/whoami with the minted access token."));

        var mintedForVictim = whoAmI.ResponseBody.Contains(victimEmail, StringComparison.OrdinalIgnoreCase);

        return ProbeResult.Failed(
            mintedForVictim
                ? $"Takeover demonstrated: an unverified email claim from an unrecognised subject minted a session for the existing account '{victimEmail}' - a property of the naive resolver a host could plausibly choose, not a framework defect."
                : "Got a 200 and an access token, but harness/whoami did not confirm it belongs to the victim account - inconclusive as a takeover demonstration.",
            evidence);
    }
}
