using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// The same valid external token, exchanged several times in a row, must mint exactly one local
/// session - every presentation after the first is the same token being replayed, and must be
/// rejected the same way an invalid one would be.
/// </summary>
internal sealed class ExtReplayProbe : IAuthProbe
{
    private const int Attempts = 5;

    public string Id => "EXT-REPLAY";
    public string Category => "External token exchange";
    public string Attack => $"The same valid, unexpired external token exchanged {Attempts} times in a row.";
    public string Expectation => "Exactly one of the exchanges succeeds; the rest are rejected the same way an invalid token would be.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost);
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        var evidence = new List<ProbeEvidence>();
        var successfulSessions = 0;
        var refreshTokens = new HashSet<string>(StringComparer.Ordinal);

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
            evidence.Add(ExternalExchangeSupport.BuildEvidence($"Replay attempt {attempt} of {Attempts}, same token.", token, exchange));

            if (exchange.StatusCode != System.Net.HttpStatusCode.OK)
                continue;

            successfulSessions++;

            if (CoreSessionSupport.TryDeserialize<LoginResponse>(exchange.ResponseBody, out var tokens) && tokens is not null)
                refreshTokens.Add(tokens.RefreshToken);
        }

        if (successfulSessions != 1)
            return ProbeResult.Failed($"{successfulSessions} of {Attempts} replays of the same provider token were exchanged for a local session, minting {refreshTokens.Count} distinct refresh token(s). Expected exactly 1.", evidence);

        return ProbeResult.Passed($"Exactly 1 of {Attempts} replays of the same provider token was exchanged for a local session; the other {Attempts - 1} were rejected.", evidence);
    }
}

