namespace BlazorBase.Dashboard.Test.Infrastructure;

/// <summary>
/// An <see cref="IDashboardLayoutStore"/> that records every call and can be told to fail or to hang.
/// </summary>
public sealed class RecordingLayoutStore : IDashboardLayoutStore
{
    public DashboardLayout Stored { get; set; } = new();

    public Exception? GetFailure { get; set; }

    public Exception? SaveFailure { get; set; }

    public Exception? ResetFailure { get; set; }

    public TaskCompletionSource? GetGate { get; set; }

    public List<string> GetKeys { get; } = [];

    public List<(string Key, DashboardLayout Layout)> Saves { get; } = [];

    public List<string> Resets { get; } = [];

    public async Task<DashboardLayout> GetAsync(string dashboardKey, CancellationToken cancellationToken = default)
    {
        GetKeys.Add(dashboardKey);

        if (GetGate is { } gate)
            await gate.Task.WaitAsync(cancellationToken);

        return GetFailure is null ? Stored : throw GetFailure;
    }

    public Task SaveAsync(string dashboardKey, DashboardLayout layout, CancellationToken cancellationToken = default)
    {
        if (SaveFailure is not null)
            return Task.FromException(SaveFailure);

        Saves.Add((dashboardKey, layout));
        Stored = layout;
        return Task.CompletedTask;
    }

    public Task ResetAsync(string dashboardKey, CancellationToken cancellationToken = default)
    {
        if (ResetFailure is not null)
            return Task.FromException(ResetFailure);

        Resets.Add(dashboardKey);
        Stored = new DashboardLayout();
        return Task.CompletedTask;
    }
}
