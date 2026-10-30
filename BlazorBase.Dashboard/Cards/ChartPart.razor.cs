using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using BlazorBase.Dashboard.Localization;

namespace BlazorBase.Dashboard;

/// <summary>
/// A card around a <c>BlazorBase.Chart</c> chart (<c>BarChart</c>, <c>LineChart</c>, <c>PieChart</c>) with a fixed responsive
/// height. The chart fragment receives the colours of the current theme and is rendered again when the theme changes, so
/// light and dark mode both stay readable.
/// </summary>
public partial class ChartPart(IStringLocalizer<DashboardResources> localizer, DashboardInterop interop) : IAsyncDisposable
{
    /// <summary>
    /// Style for the chart components inside a part: fills the fixed-height area of the card.
    /// </summary>
    public const string FillStyle = "width: 100%; height: 100%;";

    #region Injects

    private readonly IStringLocalizer<DashboardResources> Localizer = localizer;
    private readonly DashboardInterop Interop = interop;

    #endregion

    #region Parameters

    /// <summary>
    /// The heading of the card.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// Target of the "more" link, for example the full report.
    /// </summary>
    [Parameter]
    public string? MoreHref { get; set; }

    /// <summary>
    /// Text of the "more" link; defaults to the localized "Show all".
    /// </summary>
    [Parameter]
    public string? MoreText { get; set; }

    /// <summary>
    /// The chart, as a template that gets the colours of the current theme: <c>&lt;BarChart Style="@ChartPart.FillStyle" ... /&gt;</c>.
    /// </summary>
    [Parameter]
    public RenderFragment<ChartPalette>? ChartContent { get; set; }

    /// <summary>
    /// The chart, when it does not need the theme colours. Rendered together with <see cref="ChartContent"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// The height of the chart area as a CSS length; default <c>220px</c>. On phones it is capped at 200 px.
    /// </summary>
    [Parameter]
    public string Height { get; set; } = "220px";

    /// <summary>
    /// The text alternative of the chart for screen readers, for example the figures in a sentence. Defaults to the title.
    /// </summary>
    [Parameter]
    public string? Summary { get; set; }

    /// <summary>
    /// Shows a skeleton instead of the chart while its data loads.
    /// </summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// Extra content in the card header, left of the "more" link.
    /// </summary>
    [Parameter]
    public RenderFragment? Tools { get; set; }

    /// <summary>
    /// Extra CSS classes for the card.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    #endregion

    private ChartPalette? Palette { get; set; }

    private IAsyncDisposable? ThemeSubscription;

    private bool IsDisposed;

    private string AccessibleName => Summary ?? (String.IsNullOrWhiteSpace(Title) ? Localizer["ChartFallbackLabel"].Value : Title);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        Palette = await Interop.ReadChartPaletteAsync();

        if (IsDisposed)
            return;

        ThemeSubscription = await Interop.ObserveThemeAsync(OnThemeChangedAsync);
        StateHasChanged();
    }

    private async Task OnThemeChangedAsync()
    {
        var palette = await Interop.ReadChartPaletteAsync();

        await InvokeAsync(() =>
        {
            Palette = palette;
            StateHasChanged();
        });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;

        if (ThemeSubscription is not null)
            await ThemeSubscription.DisposeAsync();
    }
}
