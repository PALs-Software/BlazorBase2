namespace BlazorBase.User.SecurityHarness.Probes;

/// <summary>
/// Samples two request functions in strict alternation - A, B, A, B, ... - instead of running all of
/// A followed by all of B. A block-sampled A/B comparison confounds "which side channel exists"
/// with "which side happened to run during a transient burst of unrelated system load", since a GC
/// pause or a background process spike that lands during one whole block but not the other looks
/// exactly like a genuine timing difference. Interleaving means both groups share the same window of
/// external noise in roughly equal measure, so what remains is only the timing difference the probe
/// is actually trying to measure.
/// </summary>
internal static class InterleavedTimingSampler
{
    public static async Task<(List<double> SamplesA, List<double> SamplesB)> SampleAsync(
        Func<CancellationToken, Task> requestA,
        Func<CancellationToken, Task> requestB,
        int sampleCount,
        CancellationToken cancellationToken)
    {
        var samplesA = new List<double>(sampleCount);
        var samplesB = new List<double>(sampleCount);
        var stopwatch = new System.Diagnostics.Stopwatch();

        for (var sample = 0; sample < sampleCount; sample++)
        {
            stopwatch.Restart();
            await requestA(cancellationToken);
            stopwatch.Stop();
            samplesA.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            await requestB(cancellationToken);
            stopwatch.Stop();
            samplesB.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return (samplesA, samplesB);
    }
}

/// <summary>
/// One <see cref="TimingSideChannelAnalysis"/> verdict: whether the bootstrap confidence interval on
/// the difference of medians excludes zero, alongside both groups' median and interquartile range
/// for reporting, and the interval itself.
/// </summary>
internal sealed record TimingSideChannelVerdict(
    bool SideChannelDemonstrated,
    double MedianA,
    double InterquartileRangeA,
    double MedianB,
    double InterquartileRangeB,
    double LowerBound,
    double UpperBound)
{
    public double Delta => MedianA - MedianB;
}

/// <summary>
/// Turns two groups of raw timing samples into a stable verdict on whether a measurable side
/// channel exists, using a bootstrap confidence interval on the difference of medians rather than
/// comparing one point estimate of the delta against one point estimate of the interquartile range.
/// The latter flips its verdict run to run whenever either point estimate happens to drift, because
/// both are themselves noisy estimates with their own sampling error - resampling thousands of times
/// and asking whether the resulting interval straddles zero averages that noise out.
/// </summary>
/// <remarks>
/// Also discards each group's earliest samples before analysis - connection setup and JIT warm-up
/// bias exactly the first few requests of a run, not the steady-state cost a timing probe is trying
/// to measure - without sending any extra request, since the discarded samples were already
/// collected as part of the group.
/// </remarks>
internal static class TimingSideChannelAnalysis
{
    private const int MaxWarmupSamplesDiscarded = 5;
    private const int BootstrapResamples = 4000;
    private const double ConfidenceLevel = 0.99;

    public static TimingSideChannelVerdict Evaluate(IReadOnlyList<double> samplesA, IReadOnlyList<double> samplesB)
    {
        var trimmedA = DiscardWarmup(samplesA);
        var trimmedB = DiscardWarmup(samplesB);

        var (lowerBound, upperBound) = BootstrapMedianDeltaInterval(trimmedA, trimmedB);
        var demonstrated = lowerBound > 0 || upperBound < 0;

        return new TimingSideChannelVerdict(
            demonstrated,
            Median(trimmedA),
            InterquartileRange(trimmedA),
            Median(trimmedB),
            InterquartileRange(trimmedB),
            lowerBound,
            upperBound);
    }

    private static IReadOnlyList<double> DiscardWarmup(IReadOnlyList<double> samples)
    {
        var warmupCount = Math.Min(MaxWarmupSamplesDiscarded, samples.Count / 4);
        return warmupCount > 0 ? samples.Skip(warmupCount).ToList() : samples;
    }

    private static (double LowerBound, double UpperBound) BootstrapMedianDeltaInterval(IReadOnlyList<double> samplesA, IReadOnlyList<double> samplesB)
    {
        var random = new Random(HashCode.Combine(samplesA.Count, samplesB.Count, samplesA[0], samplesB[0]));
        var deltas = new double[BootstrapResamples];

        for (var resample = 0; resample < BootstrapResamples; resample++)
            deltas[resample] = Median(Resample(samplesA, random)) - Median(Resample(samplesB, random));

        Array.Sort(deltas);
        var tail = (1 - ConfidenceLevel) / 2;
        return (Percentile(deltas, tail), Percentile(deltas, 1 - tail));
    }

    private static List<double> Resample(IReadOnlyList<double> samples, Random random)
    {
        var resampled = new List<double>(samples.Count);

        for (var index = 0; index < samples.Count; index++)
            resampled.Add(samples[random.Next(samples.Count)]);

        return resampled;
    }

    public static double Median(IReadOnlyList<double> samples) => Percentile([.. samples.Order()], 0.50);

    public static double InterquartileRange(IReadOnlyList<double> samples)
    {
        var sorted = samples.Order().ToList();
        return Percentile(sorted, 0.75) - Percentile(sorted, 0.25);
    }

    private static double Percentile(IReadOnlyList<double> sortedSamples, double percentile)
    {
        var rank = percentile * (sortedSamples.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
            return sortedSamples[lowerIndex];

        var fraction = rank - lowerIndex;
        return sortedSamples[lowerIndex] + (sortedSamples[upperIndex] - sortedSamples[lowerIndex]) * fraction;
    }
}
