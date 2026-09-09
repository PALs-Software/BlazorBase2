using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.AccessTokens;

/// <summary>Many rapid valid calls must not each write <c>LastUsedAt</c> - the 60-second touch throttle should hold it steady within one window.</summary>
internal sealed class AccessTokenTouchNoWriteAmplificationProbe : IAuthProbe
{
    private const int RapidCalls = 20;
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(500);

    public string Id => "AT-TOUCH-NO-WRITE-AMPLIFICATION";
    public string Category => "Access tokens";
    public string Attack => $"One valid call, then {RapidCalls} more rapid valid calls with the same token inside the same minute.";
    public string Expectation => "LastUsedAt updates are throttled, not one write per request.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Low;
    public HarnessProfile Profile => HarnessProfile.AccessTokens;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        context = host.Context;

        var created = await AccessTokenSupport.CreateTokenAsync(context, "at-touch-throttle", null, null, cancellationToken);
        using var client = context.HttpClientFactory();

        var firstExchange = await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, created.Secret, cancellationToken);
        await Task.Delay(SettleDelay, cancellationToken);
        var lastUsedAfterFirst = await AccessTokenSupport.GetLastUsedAtAsync(context, created.Id, cancellationToken);

        for (var call = 1; call <= RapidCalls; call++)
            await AccessTokenSupport.CallAccessTokenProbeEndpointAsync(client, context.BaseAddress, created.Secret, cancellationToken);

        await Task.Delay(SettleDelay, cancellationToken);
        var lastUsedAfterMany = await AccessTokenSupport.GetLastUsedAtAsync(context, created.Id, cancellationToken);

        var evidence = new List<ProbeEvidence>
        {
            firstExchange.ToEvidence("First valid call."),
            new("LastUsedAt readings.", Response: $"After first call: {lastUsedAfterFirst:O}\nAfter {RapidCalls} more rapid calls: {lastUsedAfterMany:O}"),
        };

        if (lastUsedAfterFirst is null)
            return ProbeResult.Inconclusive("LastUsedAt was never set after the first call - the fire-and-forget touch had not completed within the settle delay.", evidence);

        if (lastUsedAfterMany != lastUsedAfterFirst)
            return ProbeResult.Failed($"LastUsedAt changed again within the same throttle window ({lastUsedAfterFirst:O} -> {lastUsedAfterMany:O}) - every request appears to write.", evidence);

        return ProbeResult.Passed($"LastUsedAt stayed at {lastUsedAfterFirst:O} across {RapidCalls} additional rapid calls inside the throttle window.", evidence);
    }
}
