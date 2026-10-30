using BlazorBase.CRUD.Filtering;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// What a caller sends to save a filter: create a new one, overwrite the one with the same name, or rename and
/// overwrite an existing one by <see cref="Id"/>.
/// </summary>
public sealed class SavedListFilterInput
{
    /// <summary>
    /// The filter to update. When null the filter with the same name (case-insensitive) is overwritten, or a new one is created.
    /// </summary>
    public Guid? Id { get; set; }

    public string Name { get; set; } = String.Empty;

    public FilterGroupModel Filter { get; set; } = new();

    /// <summary>
    /// Also marks the saved filter as the list's default for this user.
    /// </summary>
    public bool SetAsDefault { get; set; }
}
