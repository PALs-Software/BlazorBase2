using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.SavedFilters;
using BlazorBase.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components.Filtering;

/// <summary>
/// A dropdown next to the filter button of a list to switch between the code presets and the user's saved filters, or
/// to clear the filter, without opening the filter drawer. It renders nothing while there is no preset and no saved
/// filter to choose from.
/// </summary>
public partial class QuickFilterMenu
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    #endregion

    #region Parameters

    [Parameter, EditorRequired]
    public SavedFilterContext Saved { get; set; } = default!;

    /// <summary>
    /// The filter the list currently applies; what is marked as the active choice.
    /// </summary>
    [Parameter, EditorRequired]
    public FilterGroupModel Current { get; set; } = default!;

    /// <summary>
    /// Changing this value reloads the saved filters, e.g. after the filter drawer, where they can be edited, closed.
    /// </summary>
    [Parameter]
    public int Revision { get; set; }

    /// <summary>
    /// Raised with a cleaned copy of the chosen filter, or with null when the user chose to clear the filter.
    /// </summary>
    [Parameter]
    public EventCallback<FilterGroupModel?> OnSelect { get; set; }

    #endregion

    #region Members

    private const string NoneKey = "none";
    private const string CustomKey = "custom";
    private const string PresetPrefix = "preset:";
    private const string SavedPrefix = "saved:";

    private IStringLocalizer? ResolvedLocalizer;
    private List<SavedListFilterInfo> Items = [];
    private int LoadedRevision = -1;

    #endregion

    private IStringLocalizer Localizer =>
        ResolvedLocalizer ??= ServiceProvider.GetService<IStringLocalizerFactory>()?.Create(typeof(SavedFilterResources))
            ?? EmptyLocalizer.Instance;

    private bool HasChoices => Saved.Presets.Count > 0 || Items.Count > 0;

    internal sealed record QuickFilterChoice(string Key, string Name, bool Disabled = false);

    private IReadOnlyList<QuickFilterChoice> Choices
    {
        get
        {
            var selectedKey = SelectedKey;
            var choices = new List<QuickFilterChoice> { new(NoneKey, T("QuickFilterNone")) };

            if (selectedKey == CustomKey)
                choices.Add(new QuickFilterChoice(CustomKey, T("QuickFilterCustom"), Disabled: true));

            choices.AddRange(Saved.Presets.Select(preset => new QuickFilterChoice(PresetPrefix + preset.Id, preset.Name)));
            choices.AddRange(Items.Select(item => new QuickFilterChoice(SavedPrefix + item.Id, item.Name)));

            return choices;
        }
    }

    private bool IsUnfiltered => FilterModelConverter.CountConditions(FilterModelPruner.Prune(Current, Saved.Fields)) == 0;

    private string SelectedKey
    {
        get
        {
            if (IsUnfiltered)
                return NoneKey;

            var item = Items.FirstOrDefault(item => IsCurrent(item.Filter));

            if (item is not null)
                return SavedPrefix + item.Id;

            var preset = Saved.Presets.FirstOrDefault(preset => IsCurrent(preset.Filter));
            return preset is not null ? PresetPrefix + preset.Id : CustomKey;
        }
    }

    private string T(string key) => Localizer[key].Value;

    protected override async Task OnParametersSetAsync()
    {
        if (LoadedRevision == Revision)
            return;

        LoadedRevision = Revision;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (Saved.Store is null)
        {
            Items = [];
            return;
        }

        try
        {
            Items = [.. await Saved.ReadSavedAsync(Revision)];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Items = [];
        }
    }

    private bool IsCurrent(FilterGroupModel filter) =>
        FilterModelConverter.CountConditions(FilterModelPruner.Prune(filter, Saved.Fields)) > 0
        && ListFilterSerializer.Canonicalize(FilterModelPruner.Prune(filter, Saved.Fields))
        == ListFilterSerializer.Canonicalize(FilterModelPruner.Prune(Current, Saved.Fields));

    private async Task OnChangedAsync(string? key)
    {
        if (key is null || key == CustomKey || key == SelectedKey)
            return;

        if (key == NoneKey)
        {
            await OnSelect.InvokeAsync(null);
            return;
        }

        var filter = key.StartsWith(SavedPrefix, StringComparison.Ordinal)
            ? Items.FirstOrDefault(item => SavedPrefix + item.Id == key)?.Filter
            : Saved.Presets.FirstOrDefault(preset => PresetPrefix + preset.Id == key)?.Filter;

        if (filter is not null)
            await OnSelect.InvokeAsync(FilterModelPruner.Prune(filter, Saved.Fields));
    }
}
