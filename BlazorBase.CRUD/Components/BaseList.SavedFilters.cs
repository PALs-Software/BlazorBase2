using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.SavedFilters;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

public partial class BaseList<TModel>
{
    /// <summary>
    /// Presets, default filter and storage key declared in code. Takes precedence over
    /// <see cref="BaseListConfiguration{TModel}.FilterDefaults"/>.
    /// </summary>
    [Parameter]
    public ListFilterDefaults? FilterDefaults { get; set; }

    /// <summary>
    /// When true (the default) and the host registered an <see cref="IListFilterStore"/>, users can save, load and
    /// default their own filters in the filter drawer. Presets and code defaults work either way.
    /// </summary>
    [Parameter]
    public bool EnableSavedFilters { get; set; } = true;

    private SavedFilterContext? SavedFilters { get; set; }

    /// <summary>
    /// Builds the saved-filter context and applies the filter a first load starts with: the user's saved default,
    /// otherwise the code default.
    /// </summary>
    private async Task InitializeSavedFiltersAsync()
    {
        if (!IsFilteringEnabled)
            return;

        var context = SavedFilterContext.Create(
            ServiceProvider, typeof(TModel), FilterDefaults ?? Configuration?.FilterDefaults, FilterFields, ResolvedLocalizer, EnableSavedFilters);

        if (!context.IsAvailable)
            return;

        SavedFilters = context;

        var initial = await context.ResolveInitialFilterAsync();

        if (initial is null)
            return;

        ActiveFilterModel = initial;
        var descriptor = FilterModelConverter.ToDescriptor(initial);
        CurrentFilters = descriptor is null ? [] : [descriptor];
    }
}
