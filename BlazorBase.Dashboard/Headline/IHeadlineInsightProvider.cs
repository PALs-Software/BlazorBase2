namespace BlazorBase.Dashboard;

/// <summary>
/// Contributes insights to the headline. Register implementations with <c>AddHeadlineInsightProvider</c>; they are scoped,
/// so they can use the current user's services.
/// </summary>
public interface IHeadlineInsightProvider
{
    /// <summary>
    /// Returns the insights that apply to the current user right now; an empty list when none does. An exception is
    /// swallowed by the headline so one failing provider cannot take the others down.
    /// </summary>
    ValueTask<IReadOnlyList<HeadlineInsight>> GetInsightsAsync(CancellationToken cancellationToken);
}
