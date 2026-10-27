using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Layout.AppSwitcher;

/// <summary>
/// A website switcher for the header of <see cref="BaseLayout"/>: a chip showing the app the user is in that opens a
/// popover with grouped links to the other apps and websites.
/// </summary>
/// <remarks>
/// The content comes from the consumer through <see cref="Groups"/>; links and groups the current user may not see
/// (<see cref="AppLink.RequiredRole"/>, <see cref="AppLinkGroup.RequiredRole"/>) are left out, and so are groups that
/// end up empty. The popover is a plain <c>role="dialog"</c> region of real links, anchored below the chip on wide
/// screens and a bottom sheet with a dimmed backdrop on a phone (640 px and below). It closes on Escape, a click or
/// focus outside and after a link was chosen; focus then returns to the chip. Arrow keys, Home and End move between
/// the links. Without any visible link the chip is shown as a plain label.
/// </remarks>
public partial class BaseAppSwitcher(
    IJSRuntime jsRuntime,
    IStringLocalizer<BaseAppSwitcher> localizer) : ComponentBase, IAsyncDisposable
{
    #region Injects

    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly IStringLocalizer<BaseAppSwitcher> Localizer = localizer;

    #endregion

    /// <summary>
    /// The groups of links to offer. When empty, the links of <see cref="BaseLayout.AppLinks"/> are used, so a switcher
    /// inside the layout needs no groups of its own.
    /// </summary>
    [Parameter]
    public IReadOnlyList<AppLinkGroup> Groups { get; set; } = [];

    /// <summary>
    /// The <see cref="AppLink.Key"/> of the app the user is in; that link is marked in the popover. Defaults to
    /// <see cref="BaseLayout.CurrentAppKey"/>.
    /// </summary>
    [Parameter]
    public string? CurrentKey { get; set; }

    /// <summary>The name shown in the chip; defaults to the label of the link with <see cref="CurrentKey"/>.</summary>
    [Parameter]
    public string? CurrentLabel { get; set; }

    /// <summary>The icon shown in the chip; defaults to the icon of the link with <see cref="CurrentKey"/>.</summary>
    [Parameter]
    public string? CurrentIconSrc { get; set; }

    /// <summary>The accessible name of the chip; defaults to a localized "Switch website".</summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    [CascadingParameter]
    private AppSwitcherContext? LayoutContext { get; set; }

    private static int Counter;

    private readonly string PopoverId = $"base-app-switcher-{Interlocked.Increment(ref Counter)}";

    private IReadOnlyList<AppLinkGroup> VisibleGroups = [];
    private IReadOnlyList<IReadOnlyList<AppLinkGroup>> Columns = [];
    private IJSObjectReference? Module;
    private DotNetObjectReference<BaseAppSwitcher>? SelfReference;
    private ElementReference Root;
    private ElementReference Chip;
    private ElementReference Popover;
    private bool IsOpen;
    private bool IsAttached;

    private bool HasVisibleLinks => VisibleGroups.Count > 0;

    private IReadOnlyList<AppLinkGroup> EffectiveGroups => Groups.Count > 0 ? Groups : LayoutContext?.Groups ?? [];

    private string? EffectiveKey => CurrentKey ?? LayoutContext?.CurrentKey;

    private AppLink? CurrentLink => String.IsNullOrEmpty(EffectiveKey)
        ? null
        : EffectiveGroups.SelectMany(group => group.Links).FirstOrDefault(link => link.Key == EffectiveKey);

    private string? ResolvedLabel => CurrentLabel ?? CurrentLink?.Label;

    private string? ResolvedIconSrc => CurrentIconSrc ?? CurrentLink?.IconSrc;

    private string ChipLabel => AriaLabel
        ?? (String.IsNullOrEmpty(ResolvedLabel) ? Localizer["SwitchWebsite"] : Localizer["SwitchWebsiteFrom", ResolvedLabel]);

    private bool IsCurrent(AppLink link) => !String.IsNullOrEmpty(EffectiveKey) && link.Key == EffectiveKey;

    private string GroupTitleId(AppLinkGroup group) => $"{PopoverId}-{group.Key}";

    protected override async Task OnParametersSetAsync()
    {
        VisibleGroups = await AppLinkVisibility.FilterAsync(EffectiveGroups, AuthenticationState);
        Columns = SplitIntoColumns(VisibleGroups);

        if (!HasVisibleLinks)
            IsOpen = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsOpen == IsAttached)
            return;

        var module = await LoadModuleAsync();
        if (IsOpen)
        {
            SelfReference ??= DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("attach", Root, SelfReference);
            await module.InvokeVoidAsync("focusInitial", Popover);
        }
        else
        {
            await module.InvokeVoidAsync("detach");
        }

        IsAttached = IsOpen;
    }

    /// <summary>
    /// Called from the browser when the user pressed Escape, clicked or moved the focus outside the switcher.
    /// </summary>
    [JSInvokable]
    public Task OnDismissRequested(bool restoreFocus) => InvokeAsync(() => CloseAsync(restoreFocus));

    private Task ToggleAsync() => IsOpen ? CloseAsync(true) : OpenAsync();

    private Task OpenAsync()
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    private async Task CloseAsync(bool restoreFocus)
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        StateHasChanged();

        if (restoreFocus)
            await Chip.FocusAsync();
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            await CloseAsync(true);
    }

    private async Task<IJSObjectReference> LoadModuleAsync() =>
        Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Components/js/appSwitcher.js");

    /// <summary>
    /// Splits the groups into two columns of similar height, keeping their order: the first column gets the first
    /// groups. A group counts as its links plus one and a half rows for its title.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<AppLinkGroup>> SplitIntoColumns(IReadOnlyList<AppLinkGroup> groups)
    {
        if (groups.Count < 2)
            return groups.Count == 0 ? [] : [groups];

        var weights = groups.Select(group => group.Links.Count + 1.5).ToList();
        var total = weights.Sum();
        var bestSplit = 1;
        var bestDifference = double.MaxValue;
        var leading = 0.0;

        for (var split = 1; split < groups.Count; split++)
        {
            leading += weights[split - 1];
            var difference = Math.Abs(total - 2 * leading);
            if (difference >= bestDifference)
                continue;

            bestDifference = difference;
            bestSplit = split;
        }

        return [groups.Take(bestSplit).ToList(), groups.Skip(bestSplit).ToList()];
    }

    public async ValueTask DisposeAsync()
    {
        if (Module is not null)
        {
            try
            {
                await Module.InvokeVoidAsync("detach");
                await Module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }

            Module = null;
        }

        SelfReference?.Dispose();
        SelfReference = null;
        GC.SuppressFinalize(this);
    }
}
