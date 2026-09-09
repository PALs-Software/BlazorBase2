using System.Diagnostics;
using BlazorBase.User.SecurityHarness.Hosting;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// A well-formed, correctly signed but multi-megabyte token, bounded by a short client-side timeout
/// so a server that does hang cannot stall the whole run - the finding in that case is the timeout
/// itself.
/// </summary>
internal sealed class ExtOversizedProbe : IAuthProbe
{
    private const int OversizedClaimBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(15);

    public string Id => "EXT-OVERSIZED";
    public string Category => "External token exchange";
    public string Attack => $"A correctly signed token carrying one {OversizedClaimBytes / (1024 * 1024)} MB extra claim, bounded to {Bound.TotalSeconds:0}s.";
    public string Expectation => "Rejected without a server error and without unbounded work - report the server's behaviour and the time taken.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Medium;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        await using var host = await context.StartIsolatedHostAsync(new Dictionary<string, string?>(), cancellationToken);
        using var client = host.Context.HttpClientFactory();
        await ExternalExchangeSupport.EnsureResolvableSubjectAsync(host.Context);

        var providerHost = ExternalExchangeSupport.GetProviderHost(host.Context);
        var description = ExternalExchangeSupport.CreateValidDescription(providerHost, extraClaims: new Dictionary<string, object>
        {
            ["oversized"] = new string('a', OversizedClaimBytes),
        });
        var token = ExternalExchangeSupport.SignWithKeyA(providerHost, description);

        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(Bound);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var exchange = await ExternalExchangeSupport.ExchangeAsync(client, host.Context.BaseAddress, token, bound.Token);
            stopwatch.Stop();
            var truncatedTokenPreview = $"{token[..Math.Min(200, token.Length)]}... ({token.Length} characters total)";
            var evidence = new ProbeEvidence(
                $"A ~{OversizedClaimBytes / (1024 * 1024)} MB token, answered in {stopwatch.ElapsedMilliseconds} ms.",
                Request: $"POST api/auth/external/exchange\nToken preview: {truncatedTokenPreview}",
                Response: $"{(int)exchange.StatusCode} {exchange.StatusCode}\n{exchange.ResponseBody}");

            if (exchange.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                return ProbeResult.Failed($"The oversized token produced a 500 after {stopwatch.ElapsedMilliseconds} ms.", evidence);

            if (exchange.StatusCode == System.Net.HttpStatusCode.OK)
                return ProbeResult.Failed($"The oversized token was accepted (200) after {stopwatch.ElapsedMilliseconds} ms - no size limit rejected it.", evidence);

            return ProbeResult.Passed($"The oversized token was rejected ({(int)exchange.StatusCode} {exchange.StatusCode}) in {stopwatch.ElapsedMilliseconds} ms - well under the {Bound.TotalSeconds:0}s bound.", evidence);
        }
        catch (OperationCanceledException) when (bound.IsCancellationRequested)
        {
            stopwatch.Stop();
            return ProbeResult.Failed($"The exchange did not answer within the {Bound.TotalSeconds:0}s bound for a {OversizedClaimBytes / (1024 * 1024)} MB token - this looks like unbounded work on an oversized request.");
        }
    }
}
