using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The list a <see cref="BaseList{TModel}"/> shows instead of its table on a narrow screen: one tile per item with a
/// title, an optional subtitle, badges, a few label and value pairs, an optional action row and a more button.
/// </summary>
/// <remarks>
/// A presentation component: it renders the items and the plan it is given and reports taps; selection, the menu and
/// the data stay with the list. The tiles are virtualized: only the tiles near the visible part of the page exist,
/// and the page itself scrolls, so a list of any length costs the same.
/// </remarks>
public partial class BaseListCardView<TModel> where TModel : class
{
    #region Parameters

    [Parameter, EditorRequired]
    public BaseListCardViewModel<TModel> View { get; set; } = default!;

    #endregion

    private Virtualize<TModel>? Virtualizer { get; set; }
    private int RefreshedVersion { get; set; } = -1;

    /// <summary>
    /// Asks the virtualizer for the visible tiles again when the list loaded new data, which it does not notice
    /// by itself because its items provider stays the same.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Virtualizer is null)
            return;

        var previous = RefreshedVersion;
        RefreshedVersion = View.DataVersion;

        if (previous < 0 || previous == View.DataVersion)
            return;

        await Virtualizer.RefreshDataAsync();
        StateHasChanged();
    }

    private string ViewStyle => FormattableString.Invariant($"--base-list-tile-height: calc({View.TileHeight}rem + {BaseListTileMetrics.BorderPixels}px)");

    private bool HasActions => View.Plan.ActionColumns.Any(column => column.Template is not null) || View.Actions is not null;

    private string SortDirectionLabel => View.SortDescending ? View.Localizer["SortAscending"].Value : View.Localizer["SortDescending"].Value;

    private Icon SortDirectionIcon => View.SortDescending
        ? new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.ArrowSortDown()
        : new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.ArrowSortUp();

    private string TileClass(TModel item)
    {
        var classes = "base-list-card";

        if (View.IsSelected(item))
            classes += " is-selected";

        if (View.IsClickable)
            classes += " is-clickable";

        if (View.SelectionActive)
            classes += " is-selecting";

        return classes;
    }

    private Task OnSortPropertyChangedAsync(string? property)
        => View.OnSortChanged(property ?? String.Empty, View.SortDescending);

    private Task OnSortDirectionClickedAsync()
        => View.OnSortChanged(View.SortProperty, !View.SortDescending);

    private Task OnBodyKeyDownAsync(TModel item, KeyboardEventArgs args)
        => args.Key is "Enter" or " " ? View.OnOpen(item) : Task.CompletedTask;

    private static bool HasValue(PropertyColumnConfig<TModel> column, TModel item)
    {
        var value = column.GetValue(item);

        return value is not null && !(value is string text && text.Length == 0);
    }

    private static bool IsFlag(PropertyColumnConfig<TModel> column)
        => MobileCardPlanResolver.Classify(column) == MobileValueKind.Flag;

    private IReadOnlyList<PropertyColumnConfig<TModel>> VisibleBadges(TModel item)
        => [.. View.Plan.Badges.Where(column => column.Template is not null ? HasValue(column, item) : !String.IsNullOrEmpty(View.BadgeText(column, item)))];

    private IReadOnlyList<PropertyColumnConfig<TModel>> FieldsWithValue(TModel item)
        => [.. View.Plan.Fields.Where(column => HasValue(column, item))];

    private string ResolveFieldLabel(PropertyColumnConfig<TModel> column)
        => View.Plan.LabelFor(column) ?? View.ResolveLabel(column);

    private bool HasTitleValue(TModel item)
        => View.Plan.Title is not null && HasValue(View.Plan.Title, item);

    private bool HasSubtitleValue(TModel item)
        => View.Plan.Subtitle is not null && HasValue(View.Plan.Subtitle, item);
}
