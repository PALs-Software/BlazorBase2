namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Thrown when renaming a saved filter to a name another saved filter of the same user and list already uses.
/// </summary>
public sealed class ListFilterNameConflictException(string name)
    : Exception($"A saved filter named '{name}' already exists.")
{
    public string Name { get; } = name;
}
