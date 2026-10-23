using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using BlazorBase.Dashboard.Localization;

namespace BlazorBase.Dashboard;

/// <summary>
/// The frame of a dashboard part: a title, an optional "more" link and tools on the right, and the body.
/// </summary>
public partial class DashboardCard(IStringLocalizer<DashboardResources> localizer)
{
    #region Injects

    private readonly IStringLocalizer<DashboardResources> Localizer = localizer;

    #endregion

    #region Parameters

    /// <summary>
    /// The heading of the card (an <c>h2</c>); the card is labelled by it.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// Target of the "more" link in the header, for example the full list. No link without it.
    /// </summary>
    [Parameter]
    public string? MoreHref { get; set; }

    /// <summary>
    /// Text of the "more" link; defaults to the localized "Show all".
    /// </summary>
    [Parameter]
    public string? MoreText { get; set; }

    /// <summary>
    /// Extra content in the header, left of the "more" link: a month picker, a menu.
    /// </summary>
    [Parameter]
    public RenderFragment? Tools { get; set; }

    /// <summary>
    /// The body.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Marks the card as busy for assistive technology while its content loads.
    /// </summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// Extra CSS classes for the root element.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Further attributes for the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    #endregion

    private readonly string TitleId = $"dc-{Guid.NewGuid():N}";

    private bool HasTitle => !String.IsNullOrWhiteSpace(Title);

    private bool HasHeaderEnd => Tools is not null || !String.IsNullOrWhiteSpace(MoreHref);
}
