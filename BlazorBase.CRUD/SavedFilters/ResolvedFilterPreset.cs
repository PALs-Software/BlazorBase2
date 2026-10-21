using BlazorBase.CRUD.Filtering;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// A preset ready to show: its display name is already localized.
/// </summary>
public sealed record ResolvedFilterPreset(string Id, string Name, FilterGroupModel Filter, bool IsDefault);
