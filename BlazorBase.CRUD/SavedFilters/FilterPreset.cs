using BlazorBase.CRUD.Filtering;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// A named filter a developer declares in code and the list offers to every user.
/// </summary>
public sealed class FilterPreset
{
    /// <summary>
    /// Stable identifier. Also the localization key suffix: the display name is looked up as <c>FilterPreset_{Id}</c> in
    /// the model's resources and falls back to <see cref="Name"/>.
    /// </summary>
    public string Id { get; set; } = String.Empty;

    /// <summary>
    /// The display name used when no localized text exists.
    /// </summary>
    public string Name { get; set; } = String.Empty;

    public FilterGroupModel Filter { get; set; } = new();

    /// <summary>
    /// Applied on first load unless the user saved a default of their own.
    /// </summary>
    public bool IsDefault { get; set; }
}
