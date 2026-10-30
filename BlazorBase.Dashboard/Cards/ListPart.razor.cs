using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using BlazorBase.Dashboard.Localization;

namespace BlazorBase.Dashboard;

/// <summary>
/// A compact list of the first rows of something, with a "Show all" link to the full list. Not virtualized: it shows at most
/// <see cref="MaxItems"/> rows.
/// </summary>
/// <typeparam name="TItem">The row type.</typeparam>
public partial class ListPart<TItem>(IStringLocalizer<DashboardResources> localizer)
{
    #region Injects

    private readonly IStringLocalizer<DashboardResources> Localizer = localizer;

    #endregion

    #region Parameters

    /// <summary>
    /// The heading of the card.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// The rows; only the first <see cref="MaxItems"/> are rendered. Pass the already limited result of a server query.
    /// </summary>
    [Parameter]
    public IEnumerable<TItem>? Items { get; set; }

    /// <summary>
    /// How a row looks. It is placed inside a list item.
    /// </summary>
    [Parameter, EditorRequired]
    public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>
    /// Shown instead of the default "Nothing to show." when there are no rows.
    /// </summary>
    [Parameter]
    public RenderFragment? EmptyTemplate { get; set; }

    /// <summary>
    /// Target of the "Show all" link, usually the full list with the same filter (see <c>ListPresetLink</c>).
    /// </summary>
    [Parameter]
    public string? ShowAllHref { get; set; }

    /// <summary>
    /// Text of the "Show all" link; defaults to the localized "Show all", or "Show all (N)" when <see cref="TotalCount"/> is set.
    /// </summary>
    [Parameter]
    public string? ShowAllText { get; set; }

    /// <summary>
    /// The number of rows behind the list, when the server counted them; shown in the default link text.
    /// </summary>
    [Parameter]
    public int? TotalCount { get; set; }

    /// <summary>
    /// Maximum number of rows rendered; default 5.
    /// </summary>
    [Parameter]
    public int MaxItems { get; set; } = 5;

    /// <summary>
    /// Shows skeleton rows instead of the list while the rows load.
    /// </summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// Extra content in the card header, left of the "Show all" link.
    /// </summary>
    [Parameter]
    public RenderFragment? Tools { get; set; }

    /// <summary>
    /// Collapses the whole part (see <see cref="IDashboardPartHost.ReportEmpty"/>) while there are no rows, instead of
    /// showing the empty state.
    /// </summary>
    [Parameter]
    public bool CollapseWhenEmpty { get; set; }

    /// <summary>
    /// Extra CSS classes for the card.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    #endregion

    /// <summary>
    /// The frame of the role center, when there is one.
    /// </summary>
    [CascadingParameter]
    public IDashboardPartHost? Host { get; set; }

    private const int SkeletonRows = 3;

    private IReadOnlyList<TItem> VisibleItems { get; set; } = [];

    private string? ShowAllLabel => ShowAllText ?? (TotalCount is { } total ? String.Format(Localizer["ShowAllWithCount"].Value, total) : null);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        VisibleItems = Items is null ? [] : [.. Items.Take(Math.Max(0, MaxItems))];

        if (CollapseWhenEmpty && !Loading)
            Host?.ReportEmpty(VisibleItems.Count == 0);
    }
}
