using System.Collections.Concurrent;

namespace BlazorBase.Dashboard;

/// <summary>
/// Keeps dashboard layouts in process memory only: they are lost on reload. Meant for tests, demos and hosts that do not
/// persist anything; register it as a singleton so every component shares the data.
/// </summary>
public sealed class InMemoryDashboardLayoutStore : IDashboardLayoutStore
{
    private readonly ConcurrentDictionary<string, string> Snapshots = new();

    /// <inheritdoc />
    public Task<DashboardLayout> GetAsync(string dashboardKey, CancellationToken cancellationToken = default)
    {
        var key = DashboardLayoutValidator.ValidateKey(dashboardKey);
        return Task.FromResult(DashboardLayoutSerializer.Deserialize(Snapshots.GetValueOrDefault(key)));
    }

    /// <inheritdoc />
    public Task SaveAsync(string dashboardKey, DashboardLayout layout, CancellationToken cancellationToken = default)
    {
        var key = DashboardLayoutValidator.ValidateKey(dashboardKey);
        Snapshots[key] = DashboardLayoutSerializer.Serialize(DashboardLayoutValidator.Normalize(layout));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetAsync(string dashboardKey, CancellationToken cancellationToken = default)
    {
        var key = DashboardLayoutValidator.ValidateKey(dashboardKey);
        Snapshots.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
