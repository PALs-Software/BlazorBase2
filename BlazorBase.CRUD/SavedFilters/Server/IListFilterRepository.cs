namespace BlazorBase.CRUD.SavedFilters.Server;

/// <summary>
/// Server-side storage of saved list filters. Every operation is scoped to a user identifier: an implementation must
/// never return, change or delete a row that belongs to another user.
/// </summary>
public interface IListFilterRepository
{
    Task<IReadOnlyList<SavedListFilterInfo>> GetAsync(string userId, string listKey, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IListFilterStore.SaveAsync"/>
    Task<SavedListFilterInfo> SaveAsync(string userId, string listKey, SavedListFilterInput input, CancellationToken cancellationToken = default);

    /// <returns>False when the user owns no such filter.</returns>
    Task<bool> DeleteAsync(string userId, string listKey, Guid id, CancellationToken cancellationToken = default);

    /// <returns>False when the user owns no such filter.</returns>
    Task<bool> SetDefaultAsync(string userId, string listKey, Guid? id, CancellationToken cancellationToken = default);
}
