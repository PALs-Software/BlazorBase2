using BlazorBase.CRUD.Filtering;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// A filter a user saved for a list: its name, the filter tree and whether it is applied when the list opens.
/// </summary>
public sealed class SavedListFilterInfo
{
    public Guid Id { get; set; }

    public string Name { get; set; } = String.Empty;

    public FilterGroupModel Filter { get; set; } = new();

    /// <summary>
    /// True when this filter is applied on first load of the list instead of the code-declared default.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime ModifiedOn { get; set; }
}
