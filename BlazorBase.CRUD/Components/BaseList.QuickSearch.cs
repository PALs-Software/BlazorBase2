using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The quick search: a text box in the toolbar that narrows the list to the rows whose text properties contain what was
/// typed. The text becomes one more filter of the query, so it combines with the column filters, the saved filters, the
/// additional filters and the navigation filters, and the list loads again from its first row.
/// </summary>
public partial class BaseList<TModel> where TModel : class, new()
{
    /// <summary>
    /// Whether the list offers a search box. <c>null</c> (the default) shows it when the properties to search were named
    /// with <see cref="QuickSearchProperties"/> or <see cref="BaseListConfiguration{TModel}.QuickSearchProperties"/>,
    /// so an existing list keeps its toolbar. <c>true</c> shows it and, when no property was named, searches the model's
    /// <see cref="Attributes.DisplayKeyAttribute"/> properties. <c>false</c> hides it whatever was named. A list whose
    /// searchable properties are all unreadable for the user, or that has none, shows no box either way.
    /// </summary>
    [Parameter]
    public bool? ShowQuickSearch { get; set; }

    /// <summary>
    /// The text properties the quick search looks in, by name; a dotted path such as <c>Category.Name</c> reaches into a
    /// navigation property. Takes precedence over <see cref="BaseListConfiguration{TModel}.QuickSearchProperties"/>.
    /// Properties that do not exist, are not text or that the user may not read are ignored. A row matches when any of
    /// them contains the text.
    /// </summary>
    [Parameter]
    public List<string>? QuickSearchProperties { get; set; }

    /// <summary>
    /// How long typing has to pause, in milliseconds, before the quick search loads the list again (default 300).
    /// </summary>
    [Parameter]
    public int QuickSearchDebounceMilliseconds { get; set; } = BaseListQuickSearch.DefaultDebounceMilliseconds;

    private string QuickSearchText { get; set; } = string.Empty;
    private List<string>? QuickSearchPropertyCache { get; set; }

    private List<string> ResolvedQuickSearchProperties => QuickSearchPropertyCache ??= ResolveQuickSearchProperties();

    private bool ShowsQuickSearch => ResolvedQuickSearchProperties.Count > 0;

    private string QuickSearchPlaceholder => FrameworkLocalizer["ListSearchPlaceholder"].Value;

    private string QuickSearchClearLabel => FrameworkLocalizer["ListSearchClear"].Value;

    private List<string> ResolveQuickSearchProperties()
    {
        if (ShowQuickSearch == false)
            return [];

        var requested = QuickSearchProperties is { Count: > 0 } ? QuickSearchProperties : Configuration?.QuickSearchProperties;

        if (requested is not { Count: > 0 })
            requested = ShowQuickSearch == true ? ResolveExplicitDisplayKeyNames() : [];

        return QuickSearchPropertyResolver.Resolve(typeof(TModel), requested, CurrentUser);
    }

    private void InvalidateQuickSearchProperties() => QuickSearchPropertyCache = null;

    private FilterDescriptor? BuildQuickSearchFilter() =>
        QuickSearchFilterBuilder.Build(ResolvedQuickSearchProperties, QuickSearchText);

    private async Task OnQuickSearchChangedAsync(string searchText)
    {
        if (string.Equals(QuickSearchText, searchText, StringComparison.Ordinal))
            return;

        QuickSearchText = searchText;

        await LoadDataAsync();
        await ScrollGridToTopAsync();
    }
}
