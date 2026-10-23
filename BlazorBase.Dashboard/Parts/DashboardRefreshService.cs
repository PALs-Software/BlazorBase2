namespace BlazorBase.Dashboard;

/// <summary>
/// The <see cref="IDashboardRefreshService"/> a <c>RoleCenter</c> cascades to its parts.
/// </summary>
/// <remarks>
/// A refresh is fail-soft: an exception of one part never stops the others. Parts framed by the role center show their retry
/// card; components registered through <see cref="Register(string, IDashboardRefreshable)"/> are expected to handle their own errors.
/// </remarks>
public sealed class DashboardRefreshService : IDashboardRefreshService
{
    private sealed record Entry(string Id, Func<IDashboardRefreshable?> Resolve, Action<Exception>? OnError);

    private readonly Lock SyncRoot = new();
    private readonly List<Entry> Entries = [];
    private Task? RunningAll;

    /// <summary>
    /// Called at the start of <see cref="RefreshAllAsync"/> so the role center can show parts a user gained access to and
    /// hide the ones that were taken away before the parts reload.
    /// </summary>
    public Func<Task>? BeforeRefreshAllAsync { get; set; }

    /// <summary>
    /// Raised after every <see cref="RefreshAllAsync"/> run, however it was started.
    /// </summary>
    public event Action? RefreshAllCompleted;

    /// <inheritdoc />
    public Task RefreshAllAsync()
    {
        lock (SyncRoot)
        {
            if (RunningAll is { IsCompleted: false } running)
                return running;

            return RunningAll = RunAllAsync();
        }
    }

    /// <inheritdoc />
    public Task RefreshAsync(params string[] partIds)
    {
        ArgumentNullException.ThrowIfNull(partIds);

        var wanted = partIds.ToHashSet(StringComparer.Ordinal);
        return RunAsync(Snapshot().Where(entry => wanted.Contains(entry.Id)));
    }

    /// <inheritdoc />
    public IDisposable Register(string id, IDashboardRefreshable refreshable)
    {
        ArgumentNullException.ThrowIfNull(refreshable);

        return Add(new Entry(id, () => refreshable, null));
    }

    /// <summary>
    /// Registers a part frame: the refreshable is resolved at refresh time because the part instance can be recreated.
    /// </summary>
    internal IDisposable Register(string id, Func<IDashboardRefreshable?> resolve, Action<Exception> onError) =>
        Add(new Entry(id, resolve, onError));

    private async Task RunAllAsync()
    {
        await Task.Yield();

        try
        {
            if (BeforeRefreshAllAsync is { } before)
                await SafeAsync(before);

            await RunAsync(Snapshot());
        }
        finally
        {
            RefreshAllCompleted?.Invoke();
        }
    }

    private static async Task SafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }

    private static Task RunAsync(IEnumerable<Entry> entries) =>
        Task.WhenAll(entries.Select(RunEntryAsync).ToArray());

    private static async Task RunEntryAsync(Entry entry)
    {
        var refreshable = entry.Resolve();

        if (refreshable is null)
            return;

        try
        {
            await refreshable.RefreshAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            entry.OnError?.Invoke(exception);
        }
    }

    private Entry[] Snapshot()
    {
        lock (SyncRoot)
            return [.. Entries];
    }

    private IDisposable Add(Entry entry)
    {
        lock (SyncRoot)
            Entries.Add(entry);

        return new Registration(this, entry);
    }

    private void Remove(Entry entry)
    {
        lock (SyncRoot)
            Entries.Remove(entry);
    }

    private sealed class Registration(DashboardRefreshService owner, Entry entry) : IDisposable
    {
        public void Dispose() => owner.Remove(entry);
    }
}
