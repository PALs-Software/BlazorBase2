namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// An <see cref="IDashboardPartHost"/> that records what a part reports.
/// </summary>
public sealed class RecordingHost : IDashboardPartHost
{
    public string PartId => "recording";

    public List<bool> EmptyReports { get; } = [];

    public int LoadingReports { get; private set; }

    public int LoadedReports { get; private set; }

    public void ReportLoading() => LoadingReports++;

    public void ReportLoaded() => LoadedReports++;

    public void ReportEmpty(bool isEmpty = true) => EmptyReports.Add(isEmpty);
}
