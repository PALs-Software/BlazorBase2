using System.Collections.Concurrent;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Keeps saved filters in process memory only: they are lost on reload. Meant for tests, demos and hosts that do not
/// persist anything; register it as a singleton so every component shares the data.
/// </summary>
public sealed class InMemoryListFilterStore : SnapshotListFilterStore
{
    private readonly ConcurrentDictionary<string, string> Snapshots = new();

    protected override Task<List<SavedListFilterInfo>> LoadAsync(string listKey, CancellationToken cancellationToken) =>
        Task.FromResult(ListFilterSerializer.DeserializeList(Snapshots.GetValueOrDefault(listKey)));

    protected override Task PersistAsync(string listKey, List<SavedListFilterInfo> filters, CancellationToken cancellationToken)
    {
        Snapshots[listKey] = ListFilterSerializer.SerializeList(filters);
        return Task.CompletedTask;
    }
}
