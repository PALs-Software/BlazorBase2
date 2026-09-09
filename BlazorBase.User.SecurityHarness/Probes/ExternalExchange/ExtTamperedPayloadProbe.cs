using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>A validly signed token with one payload byte flipped after signing - the signature no longer matches.</summary>
internal sealed class ExtTamperedPayloadProbe : IAuthProbe
{
    public string Id => "EXT-TAMPERED-PAYLOAD";
    public string Category => "External token exchange";
    public string Attack => "A validly signed token with one character of the payload segment changed after signing.";
    public string Expectation => "Rejected.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Critical;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost);
        var validToken = ExternalExchangeSupport.SignWithKeyA(providerHost, description);
        var token = TamperOnePayloadCharacter(validToken);

        var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, cancellationToken);
        var evidence = ExternalExchangeSupport.BuildEvidence("A validly signed token with one payload character changed after signing.", token, exchange);

        if (exchange.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"Expected 401, got {(int)exchange.StatusCode} {exchange.StatusCode}. A tampered payload was not rejected.", evidence);

        return ProbeResult.Passed("A validly signed token with one tampered payload character was rejected.", evidence);
    }

    private static string TamperOnePayloadCharacter(string token)
    {
        var segments = token.Split('.');
        var payloadChars = segments[1].ToCharArray();
        var position = payloadChars.Length / 2;
        payloadChars[position] = payloadChars[position] == 'A' ? 'B' : 'A';
        segments[1] = new string(payloadChars);

        return string.Join('.', segments);
    }
}
