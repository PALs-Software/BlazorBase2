namespace BlazorBase.Dashboard;

/// <summary>
/// Cascaded by the role center to every part: lets a part tell the frame around it what state it is in.
/// </summary>
public interface IDashboardPartHost
{
    /// <summary>
    /// The registered id of the part this host frames.
    /// </summary>
    string PartId { get; }

    /// <summary>
    /// Shows the loading skeleton over the part until <see cref="ReportLoaded"/> is called.
    /// </summary>
    void ReportLoading();

    /// <summary>
    /// Removes the loading skeleton.
    /// </summary>
    void ReportLoaded();

    /// <summary>
    /// Collapses the part completely (no frame, no gap in the grid) while <paramref name="isEmpty"/> is true. Call it again
    /// with false when the part has something to show.
    /// </summary>
    void ReportEmpty(bool isEmpty = true);
}
