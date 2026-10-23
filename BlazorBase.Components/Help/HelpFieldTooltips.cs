using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Help;

/// <summary>
/// Shows the field tooltips of the contextual help: while it is rendered, hovering or focusing a label of the page
/// whose text matches an entry below the <c>&lt;!-- help:fields --&gt;</c> marker of the page's help topic shows a small popover with the entry's first
/// sentence and a "More in help" link, which raises <see cref="OnOpenHelp"/>. It renders nothing itself.
/// </summary>
/// <remarks>
/// <see cref="BlazorBase.Components.Layout.BaseLayout"/> renders it next to the help panel when <c>ShowHelp</c> is on, <see cref="HelpOptions.ShowFieldTooltips"/>
/// is on and <see cref="IHelpFieldIndex"/> is registered. The script (<c>helpTooltips.js</c>) listens on the document
/// with delegated handlers, asks the component for the tips of the current page once per page and culture the first time
/// the pointer rests on a short text, and matches texts in the browser. Its listeners and the popover go away with
/// the component; touch input shows no tooltip and the popover never intercepts clicks.
/// </remarks>
public sealed class HelpFieldTooltips(
    NavigationManager navigationManager,
    IHelpFieldIndex fieldIndex,
    IJSRuntime jsRuntime,
    IStringLocalizer<BaseHelpPanel> localizer) : ComponentBase, IAsyncDisposable
{
    private const int MaxAnchorLength = 200;

    #region Injects

    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IHelpFieldIndex FieldIndex = fieldIndex;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly IStringLocalizer<BaseHelpPanel> Localizer = localizer;

    #endregion

    /// <summary>Raised when the user follows the "More in help" link of a tooltip; the host opens the help panel at that place.</summary>
    [Parameter]
    public EventCallback<HelpAnchorTarget> OnOpenHelp { get; set; }

    /// <summary>The UI culture the texts are shown in; the script forgets what it loaded when it changes.</summary>
    [Parameter]
    public string? CultureName { get; set; }

    /// <summary>A CSS selector for the areas whose labels get tooltips: the main content and the dialogs of the application shell by default; never the help panel.</summary>
    [Parameter]
    public string ScopeSelector { get; set; } = "#base-main, fluent-dialog";

    #region Members

    private IJSObjectReference? Module;
    private DotNetObjectReference<HelpFieldTooltips>? SelfReference;
    private string? AppliedCulture;
    private bool IsDisposed;

    #endregion

    private string CurrentPath
    {
        get
        {
            var path = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
            var end = path.IndexOfAny(['?', '#']);
            return end < 0 ? path : path[..end];
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsDisposed)
            return;

        if (firstRender)
        {
            Module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorBase.Components/js/helpTooltips.js");
            SelfReference = DotNetObjectReference.Create(this);
            NavigationManager.LocationChanged += OnLocationChanged;
            await EnableAsync();
            return;
        }

        if (Module is not null && !string.Equals(AppliedCulture, CultureName, StringComparison.Ordinal))
            await EnableAsync();
    }

    /// <summary>Called by the script the first time a label is hovered on a page: returns the tips of the page's help topic, or null when there are none.</summary>
    [JSInvokable]
    public Task<HelpFieldIndexData?> GetFieldIndexAsync() => FieldIndex.GetForPathAsync(CurrentPath);

    /// <summary>Called by the script when the user follows the "More in help" link of a tooltip.</summary>
    /// <param name="topic">The topic the tip comes from.</param>
    /// <param name="anchor">The id of the field entry in the topic.</param>
    [JSInvokable]
    public Task OpenHelpAsync(string topic, string anchor) => InvokeAsync(() =>
    {
        if (!HelpPaths.IsSafe(topic) || string.IsNullOrWhiteSpace(anchor) || anchor.Length > MaxAnchorLength)
            return Task.CompletedTask;

        return OnOpenHelp.InvokeAsync(new HelpAnchorTarget(topic, anchor));
    });

    private async Task EnableAsync()
    {
        if (Module is null || SelfReference is null)
            return;

        AppliedCulture = CultureName;
        await Module.InvokeVoidAsync("enableFieldTooltips", SelfReference, new { moreLabel = Localizer["MoreInHelp"].Value, scopeSelector = ScopeSelector });
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => _ = HideAsync();

    private async Task HideAsync()
    {
        if (Module is null)
            return;

        try
        {
            await Module.InvokeVoidAsync("hideFieldTooltip");
        }
        catch (Exception exception) when (exception is JSDisconnectedException or ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        NavigationManager.LocationChanged -= OnLocationChanged;

        if (Module is not null)
        {
            try
            {
                await Module.InvokeVoidAsync("disableFieldTooltips");
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
