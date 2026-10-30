namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// An insight provider that counts how often it was asked.
/// </summary>
public sealed class CountingInsightProvider : IHeadlineInsightProvider
{
    public int Calls { get; private set; }

    public ValueTask<IReadOnlyList<HeadlineInsight>> GetInsightsAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return ValueTask.FromResult<IReadOnlyList<HeadlineInsight>>([new HeadlineInsight(1, "**One** insight")]);
    }
}
