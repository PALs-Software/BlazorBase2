using System.Net;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness.Probes.AccountManagement;

/// <summary>
/// Shared plumbing every account-management probe uses: generating the password-reset and
/// email-confirmation tokens <c>AccountEndpointMapper</c> would otherwise only hand out through an
/// email nobody here can read - the default <c>IEmailSender&lt;TUser&gt;</c> is <c>NoOpEmailSender</c>,
/// which only logs - directly through <see cref="UserManager{TUser}"/> against the profile's own
/// service provider, and sampling an endpoint's status, body and latency for the enumeration probes.
/// </summary>
internal static class AccountManagementSupport
{
    public static async Task<string> GeneratePasswordResetTokenAsync(ProbeContext context, HarnessUser user)
    {
        using var scope = context.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<HarnessUser>>();
        return await userManager.GeneratePasswordResetTokenAsync(user);
    }

    public static async Task<string> GenerateEmailConfirmationTokenAsync(ProbeContext context, HarnessUser user)
    {
        using var scope = context.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<HarnessUser>>();
        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    /// <summary>
    /// Samples <paramref name="pathA"/> and <paramref name="pathB"/> - the same path, a known and an
    /// unknown address - alternating one request at a time via <see cref="InterleavedTimingSampler"/>,
    /// recording the status, raw body and elapsed time of each side. The raw material both
    /// enumeration probes reduce to a status/body comparison and a bootstrap-based timing verdict.
    /// </summary>
    public static async Task<(EnumerationSample A, EnumerationSample B)> SampleEndpointPairAsync(
        HttpClient client,
        Uri baseAddress,
        string path,
        Func<object> requestFactoryA,
        Func<object> requestFactoryB,
        int sampleCount,
        CancellationToken cancellationToken)
    {
        var statusesA = new List<HttpStatusCode>(sampleCount);
        var bodiesA = new List<string>(sampleCount);
        var statusesB = new List<HttpStatusCode>(sampleCount);
        var bodiesB = new List<string>(sampleCount);

        var (samplesA, samplesB) = await InterleavedTimingSampler.SampleAsync(
            async ct =>
            {
                var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, baseAddress, path, requestFactoryA(), cancellationToken: ct);
                statusesA.Add(exchange.StatusCode);
                bodiesA.Add(exchange.ResponseBody);
            },
            async ct =>
            {
                var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, baseAddress, path, requestFactoryB(), cancellationToken: ct);
                statusesB.Add(exchange.StatusCode);
                bodiesB.Add(exchange.ResponseBody);
            },
            sampleCount, cancellationToken);

        return (new EnumerationSample(statusesA, bodiesA, samplesA), new EnumerationSample(statusesB, bodiesB, samplesB));
    }
}

/// <summary>One enumeration group's raw samples: every response's status and body, and every request's elapsed time.</summary>
internal sealed record EnumerationSample(IReadOnlyList<HttpStatusCode> Statuses, IReadOnlyList<string> Bodies, IReadOnlyList<double> Samples);

/// <summary>
/// Reduces two <see cref="EnumerationSample"/> groups (a known address and an unknown one) to one
/// <see cref="ProbeResult"/>: a hard failure when the status or body differs at all between the
/// groups, otherwise a bootstrap-based timing comparison via <see cref="TimingSideChannelAnalysis"/>
/// - <see cref="ProbeOutcome.Inconclusive"/> when the interval straddles zero, since "no side channel
/// demonstrated" is not the same claim as "no side channel exists".
/// </summary>
internal static class AccountEnumerationVerdict
{
    public static ProbeResult Evaluate(string endpointName, EnumerationSample known, EnumerationSample unknown)
    {
        var evidence = new ProbeEvidence(
            "Raw sample latencies, in milliseconds.",
            Request: $"POST api/auth/manage/{endpointName} x{known.Samples.Count} (known address) and x{unknown.Samples.Count} (unknown address)",
            Response: $"known: [{string.Join(", ", known.Samples.Select(sample => sample.ToString("F2")))}]\n" +
                      $"unknown: [{string.Join(", ", unknown.Samples.Select(sample => sample.ToString("F2")))}]");

        if (known.Statuses.Distinct().Count() > 1 || unknown.Statuses.Distinct().Count() > 1 || known.Statuses[0] != unknown.Statuses[0])
            return ProbeResult.Failed($"Status codes differ between the two groups: known {string.Join(",", known.Statuses.Distinct())}, unknown {string.Join(",", unknown.Statuses.Distinct())}.", evidence);

        if (known.Bodies.Distinct().Count() > 1 || unknown.Bodies.Distinct().Count() > 1 || known.Bodies[0] != unknown.Bodies[0])
            return ProbeResult.Failed("Response bodies differ between the two groups - an enumeration oracle in the body.", evidence);

        var verdict = TimingSideChannelAnalysis.Evaluate(known.Samples, unknown.Samples);

        var summary =
            $"Both groups answered {known.Statuses[0]} with an identical body. {known.Samples.Count} samples each: " +
            $"known-address median {verdict.MedianA:F2} ms (IQR {verdict.InterquartileRangeA:F2} ms); unknown-address median {verdict.MedianB:F2} ms (IQR {verdict.InterquartileRangeB:F2} ms); delta {verdict.Delta:F2} ms " +
            $"(99% bootstrap interval [{verdict.LowerBound:F2}, {verdict.UpperBound:F2}] ms).";

        if (verdict.SideChannelDemonstrated)
            return ProbeResult.Failed($"{summary} The interval excludes zero - a timing side channel survives the identical response.", evidence);

        return ProbeResult.Inconclusive($"{summary} The interval includes zero - no timing side channel demonstrated.", evidence);
    }
}

