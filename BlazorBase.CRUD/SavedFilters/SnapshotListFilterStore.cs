namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Shared behaviour of the stores that keep a list's filters as one snapshot (browser storage, memory): read the
/// snapshot, change it, write it back. A snapshot is private to one browser or process, so there is no user dimension.
/// </summary>
public abstract class SnapshotListFilterStore : IListFilterStore
{
    protected abstract Task<List<SavedListFilterInfo>> LoadAsync(string listKey, CancellationToken cancellationToken);

    protected abstract Task PersistAsync(string listKey, List<SavedListFilterInfo> filters, CancellationToken cancellationToken);

    public async Task<IReadOnlyList<SavedListFilterInfo>> GetAsync(string listKey, CancellationToken cancellationToken = default)
    {
        var key = ListFilterValidator.ValidateListKey(listKey);
        var filters = await LoadAsync(key, cancellationToken);
        return [.. filters.OrderBy(filter => filter.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<SavedListFilterInfo> SaveAsync(string listKey, SavedListFilterInput input, CancellationToken cancellationToken = default)
    {
        var key = ListFilterValidator.ValidateListKey(listKey);
        var name = ListFilterValidator.NormalizeName(input.Name);
        ListFilterValidator.ValidateFilter(input.Filter);

        var filters = await LoadAsync(key, cancellationToken);
        var sameName = filters.FirstOrDefault(filter => String.Equals(filter.Name, name, StringComparison.OrdinalIgnoreCase));
        var target = FindTarget(filters, input, sameName);

        if (target is null)
        {
            if (filters.Count >= ListFilterValidator.MaxFiltersPerList)
                throw new ListFilterValidationException($"At most {ListFilterValidator.MaxFiltersPerList} filters can be saved per list.");

            target = new SavedListFilterInfo { Id = Guid.NewGuid() };
            filters.Add(target);
        }

        target.Name = name;
        target.Filter = ListFilterSerializer.Deserialize(ListFilterSerializer.Serialize(input.Filter));
        target.ModifiedOn = DateTime.UtcNow;

        if (input.SetAsDefault)
            MarkDefault(filters, target.Id);

        await PersistAsync(key, filters, cancellationToken);
        return target;
    }

    public async Task<bool> DeleteAsync(string listKey, Guid id, CancellationToken cancellationToken = default)
    {
        var key = ListFilterValidator.ValidateListKey(listKey);
        var filters = await LoadAsync(key, cancellationToken);

        if (filters.RemoveAll(filter => filter.Id == id) == 0)
            return false;

        await PersistAsync(key, filters, cancellationToken);
        return true;
    }

    public async Task<bool> SetDefaultAsync(string listKey, Guid? id, CancellationToken cancellationToken = default)
    {
        var key = ListFilterValidator.ValidateListKey(listKey);
        var filters = await LoadAsync(key, cancellationToken);

        if (id is { } wanted && filters.All(filter => filter.Id != wanted))
            return false;

        MarkDefault(filters, id);
        await PersistAsync(key, filters, cancellationToken);
        return true;
    }

    private static SavedListFilterInfo? FindTarget(List<SavedListFilterInfo> filters, SavedListFilterInput input, SavedListFilterInfo? sameName)
    {
        if (input.Id is not { } id)
            return sameName;

        var byId = filters.FirstOrDefault(filter => filter.Id == id)
            ?? throw new KeyNotFoundException("The saved filter no longer exists.");

        if (sameName is not null && sameName.Id != byId.Id)
            throw new ListFilterNameConflictException(sameName.Name);

        return byId;
    }

    private static void MarkDefault(List<SavedListFilterInfo> filters, Guid? id)
    {
        foreach (var filter in filters)
            filter.IsDefault = filter.Id == id;
    }
}
