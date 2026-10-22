using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Everything <see cref="BaseListCardView{TModel}"/> needs from the list that hosts it. The view only draws;
/// the list keeps selection, the data and the menu.
/// </summary>
public sealed class BaseListCardViewModel<TModel> where TModel : class
{
    public required ItemsProviderDelegate<TModel> ItemsProvider { get; init; }

    /// <summary>How many items the list holds in total; the tile view shows nothing while it is zero.</summary>
    public int TotalCount { get; init; }

    /// <summary>Changes whenever the list loaded its data again, which makes the tile view ask for the visible tiles anew.</summary>
    public int DataVersion { get; init; }

    /// <summary>The height of a tile in rem, without its border. Every tile has this height, which is what lets the virtualizer place the rest.</summary>
    public double TileHeight { get; init; } = BaseListTileMetrics.CustomBodyHeightRem;

    /// <summary>The height of a tile's slot in the list, in pixels: the tile, its border and the gap below it.</summary>
    public float ItemSize => (float)BaseListTileMetrics.ToSlotPixels(TileHeight);

    public required MobileCardPlan<TModel> Plan { get; init; }

    public required IStringLocalizer Localizer { get; init; }

    /// <summary>Custom tile body; replaces title, subtitle, badges and fields.</summary>
    public RenderFragment<TModel>? Template { get; init; }

    /// <summary>An action row from the tile configuration, rendered below the columns with the role Actions.</summary>
    public RenderFragment<TModel>? Actions { get; init; }

    /// <summary>Whether tapping a tile does anything (the list opens a card, or the page listens to the selection).</summary>
    public bool IsClickable { get; init; }

    public bool SelectionActive { get; init; }

    public required Func<TModel, bool> IsSelected { get; init; }

    public required Func<TModel, Task> OnOpen { get; init; }

    public required Action<TModel> OnToggleSelect { get; init; }

    public required Func<TModel, MouseEventArgs, Task> OnMenu { get; init; }

    public required Func<TModel, string> ResolveFallbackTitle { get; init; }

    public required Func<PropertyColumnConfig<TModel>, string> ResolveLabel { get; init; }

    public required Func<PropertyColumnConfig<TModel>, TModel, RenderFragment> RenderValue { get; init; }

    /// <summary>The text of a badge: the title of a boolean column, otherwise the displayed value, which is empty when the item has nothing to show for it.</summary>
    public required Func<PropertyColumnConfig<TModel>, TModel, string?> BadgeText { get; init; }

    /// <summary>The properties the user can sort by; empty hides the sort selector.</summary>
    public IReadOnlyList<MobileSortOption> SortOptions { get; init; } = [];

    /// <summary>The property the list is sorted by; empty while the list's own order applies.</summary>
    public string SortProperty { get; init; } = String.Empty;

    public bool SortDescending { get; init; }

    public Func<string, bool, Task> OnSortChanged { get; init; } = (_, _) => Task.CompletedTask;
}
