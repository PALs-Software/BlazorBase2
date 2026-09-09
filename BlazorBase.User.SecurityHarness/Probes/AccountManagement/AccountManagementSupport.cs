using System.Diagnostics;
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
    /// Sends <paramref name="sampleCount"/> requests to <paramref name="path"/>, each built fresh by
    /// <paramref name="requestFactory"/>, and records the status, raw body and elapsed time of each -
    /// the raw material both enumeration probes reduce to a status/body comparison and a
    /// median/interquartile-range timing verdict.
    /// </summary>
    public static async Task<EnumerationSample> SampleEndpointAsync(
        HttpClient client,
        Uri baseAddress,
        string path,
        Func<object> requestFactory,
        int sampleCount,
        CancellationToken cancellationToken)
    {
        var statuses = new List<HttpStatusCode>(sampleCount);
        var bodies = new List<string>(sampleCount);
        var samples = new List<double>(sampleCount);
        var stopwatch = new Stopwatch();

        for (var sample = 0; sample < sampleCount; sample++)
        {
            stopwatch.Restart();
            var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, baseAddress, path, requestFactory(), cancellationToken: cancellationToken);
            stopwatch.Stop();
            statuses.Add(exchange.StatusCode);
            bodies.Add(exchange.ResponseBody);
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return new EnumerationSample(statuses, bodies, samples);
    }

    public static (double Median, double InterquartileRange) MedianAndInterquartileRange(IReadOnlyList<double> samples)
    {
        var sorted = samples.Order().ToList();
        return (Percentile(sorted, 0.50), Percentile(sorted, 0.75) - Percentile(sorted, 0.25));
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        var rank = percentile * (sorted.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
            return sorted[lowerIndex];

        var fraction = rank - lowerIndex;
        return sorted[lowerIndex] + (sorted[upperIndex] - sorted[lowerIndex]) * fraction;
    }
}

/// <summary>One enumeration group's raw samples: every response's status and body, and every request's elapsed time.</summary>
internal sealed record EnumerationSample(IReadOnlyList<HttpStatusCode> Statuses, IReadOnlyList<string> Bodies, IReadOnlyList<double> Samples);

/// <summary>
/// Reduces two <see cref="EnumerationSample"/> groups (a known address and an unknown one) to one
/// <see cref="ProbeResult"/>: a hard failure when the status or body differs at all between the
/// groups, otherwise a median/interquartile-range timing comparison in the same style as
/// <c>LOGIN-TIMING-ORACLE</c> - <see cref="ProbeOutcome.Inconclusive"/> when the delta sits inside
/// the noise, since "no side channel demonstrated" is not the same claim as "no side channel exists".
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

        var (knownMedian, knownIqr) = AccountManagementSupport.MedianAndInterquartileRange(known.Samples);
        var (unknownMedian, unknownIqr) = AccountManagementSupport.MedianAndInterquartileRange(unknown.Samples);
        var delta = Math.Abs(knownMedian - unknownMedian);
        var noiseFloor = Math.Max(knownIqr, unknownIqr);

        var summary =
            $"Both groups answered {known.Statuses[0]} with an identical body. {known.Samples.Count} samples each: " +
            $"known-address median {knownMedian:F2} ms (IQR {knownIqr:F2} ms); unknown-address median {unknownMedian:F2} ms (IQR {unknownIqr:F2} ms); delta {delta:F2} ms.";

        if (delta > noiseFloor)
            return ProbeResult.Failed($"{summary} The delta exceeds the noise floor (max IQR {noiseFloor:F2} ms) - a timing side channel survives the identical response.", evidence);

        return ProbeResult.Inconclusive($"{summary} The delta sits inside the noise (max IQR {noiseFloor:F2} ms) - no timing side channel demonstrated.", evidence);
    }
}

