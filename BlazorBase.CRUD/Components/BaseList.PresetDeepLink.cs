using BlazorBase.Components.Routing;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.SavedFilters;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

public partial class BaseList<TModel>
{
    /// <summary>
    /// The query-string parameter that names a preset to apply on the first load (see <see cref="ListPresetLink"/>).
    /// Only read while <see cref="EnableDeepLinking"/> is true.
    /// </summary>
    [Parameter]
    public string PresetParameterName { get; set; } = ListPresetLink.DefaultParameterName;

    /// <summary>
    /// The filter of the preset the page URL asks for, or null when the URL names none or an unknown one. It replaces
    /// the user's saved default and the code default for this visit only and is never stored.
    /// </summary>
    private FilterGroupModel? ResolveDeepLinkedPreset(SavedFilterContext context)
    {
        if (!EnableDeepLinking || String.IsNullOrEmpty(PresetParameterName))
            return null;

        var presetId = QueryStringReader.TryReadValue(Navigation.Uri, PresetParameterName);

        if (String.IsNullOrEmpty(presetId))
            return null;

        var preset = context.Presets.FirstOrDefault(candidate => String.Equals(candidate.Id, presetId, StringComparison.OrdinalIgnoreCase));

        return preset is null ? null : FilterModelPruner.Prune(preset.Filter, context.Fields);
    }
}
