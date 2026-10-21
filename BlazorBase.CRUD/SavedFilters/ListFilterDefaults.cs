using BlazorBase.CRUD.Filtering;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// The filters a developer declares for a list in code: the key under which user filters are stored, the presets
/// offered in the filter drawer and the filter applied on first load.
/// </summary>
public sealed class ListFilterDefaults
{
    /// <summary>
    /// Overrides the key saved filters are stored under. Null uses the model type's full name. Give two lists over the
    /// same model different keys when their saved filters should not be shared.
    /// </summary>
    public string? ListKey { get; set; }

    public List<FilterPreset> Presets { get; set; } = [];

    /// <summary>
    /// An unnamed default filter, applied on first load when no preset is flagged as default.
    /// </summary>
    public FilterGroupModel? DefaultFilter { get; set; }

    /// <summary>
    /// The filter applied on first load when the user has no saved default of their own.
    /// </summary>
    public FilterGroupModel? ResolveCodeDefault() =>
        Presets.FirstOrDefault(preset => preset.IsDefault)?.Filter ?? DefaultFilter;
}
