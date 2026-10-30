namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// Shared state the test parts read: whether they should fail, how often they were refreshed, whether they report empty and
/// the gate that holds back a loading part.
/// </summary>
public sealed class PartProbe
{
    public bool ShouldFail { get; set; }

    public bool ShouldFailOnRefresh { get; set; }

    public int Refreshes { get; set; }

    public int Loads { get; set; }

    public bool ReportsEmpty { get; set; }

    public TaskCompletionSource? LoadGate { get; set; }
}
