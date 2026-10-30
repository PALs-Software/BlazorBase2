namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Persists the filters a user saved for a list. Implementations are keyed by the list key (by default the model
/// type's full name) and always act for the current user; they never expose another user's filters.
/// </summary>
/// <remarks>
/// <see cref="HttpListFilterStore"/> talks to the endpoints of <c>MapBlazorBaseListFilters</c> and is the choice for
/// hosts with server storage; <see cref="LocalStorageListFilterStore"/> and <see cref="InMemoryListFilterStore"/> are
/// fallbacks for hosts without one.
/// </remarks>
public interface IListFilterStore
{
    /// <summary>
    /// Returns the current user's saved filters for the list, ordered by name.
    /// </summary>
    Task<IReadOnlyList<SavedListFilterInfo>> GetAsync(string listKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates, overwrites or renames a saved filter and returns the stored result.
    /// </summary>
    /// <exception cref="ListFilterValidationException">The name, key or filter is not acceptable.</exception>
    /// <exception cref="ListFilterNameConflictException">Another filter of the user already has the requested name.</exception>
    /// <exception cref="KeyNotFoundException">The input names an id the user does not own.</exception>
    Task<SavedListFilterInfo> SaveAsync(string listKey, SavedListFilterInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one of the current user's saved filters.
    /// </summary>
    /// <returns>False when the user has no such filter.</returns>
    Task<bool> DeleteAsync(string listKey, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks one saved filter as the default of the list, or clears the default when <paramref name="id"/> is null.
    /// </summary>
    /// <returns>False when the user has no such filter.</returns>
    Task<bool> SetDefaultAsync(string listKey, Guid? id, CancellationToken cancellationToken = default);
}
