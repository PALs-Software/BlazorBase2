using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Layout;

/// <summary>
/// The application shell: a header, a navigation area and the main content.
/// </summary>
/// <remarks>
/// Above 1024 px the navigation is a persistent rail that the header button reduces to icons. At 1024 px
/// and below it becomes an off-canvas drawer opened by the same button, closed again by navigating,
/// Escape or a click on the overlay; focus moves into the drawer while it is open and back to the
/// button afterwards. The shell fills the dynamic viewport and only the main content scrolls.
/// Header content can mark elements with <c>base-header-wide-only</c> to hide them at 768 px and below,
/// and navigation content can mark elements with <c>base-nav-narrow-only</c> to show them only in the
/// drawer at that width — the place to move controls that do not fit a phone header.
/// </remarks>
public partial class BaseLayout(
    IThemeService themeService,
    ILanguageService languageService,
    IJSRuntime jsRuntime,
    NavigationManager navigationManager,
    IStringLocalizer<BaseLayout> localizer) : ComponentBase, IAsyncDisposable
{
    #region Injects

    private readonly IThemeService ThemeService = themeService;
    private readonly ILanguageService LanguageService = languageService;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IStringLocalizer<BaseLayout> Localizer = localizer;

    #endregion

    [Parameter]
    public string AppTitle { get; set; } = string.Empty;

    /// <summary>Replaces the plain <see cref="AppTitle"/> text at the start of the header, e.g. with a logo.</summary>
    [Parameter]
    public RenderFragment? Brand { get; set; }

    [Parameter]
    public RenderFragment? Navigation { get; set; }

    [Parameter]
    public RenderFragment? Banner { get; set; }

    [Parameter]
    public RenderFragment? HeaderContent { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private IJSObjectReference? Module;
    private DotNetObjectReference<BaseLayout>? SelfReference;
    private FluentButton? NavigationToggle;
    private ElementReference NavigationHost;
    private ElementReference MainContent;
    private bool IsCompact;
    private bool IsDrawerOpen;
    private bool IsCollapsed;
    private bool FocusDrawerAfterRender;
    private bool RestoreFocusAfterRender;

    private DesignThemeModes CurrentMode => ThemeService.CurrentTheme switch
    {
        ThemePreference.Light => DesignThemeModes.Light,
        ThemePreference.Dark => DesignThemeModes.Dark,
        _ => DesignThemeModes.System,
    };

    private NavigationLayoutContext NavigationContext => new(IsCollapsed && !IsCompact, CloseDrawerAsync);

    private bool IsMainInert => IsDrawerOpen && IsCompact;

    private string NavigationExpandedValue => (IsCompact ? IsDrawerOpen : !IsCollapsed) ? "true" : "false";

    private string NavigationToggleLabel => (IsCompact, IsDrawerOpen, IsCollapsed) switch
    {
        (true, true, _) => Localizer["CloseNavigation"],
        (true, false, _) => Localizer["OpenNavigation"],
        (false, _, true) => Localizer["ExpandNavigation"],
        _ => Localizer["CollapseNavigation"],
    };

    protected override async Task OnInitializedAsync()
    {
        await ThemeService.InitializeAsync();
        await LanguageService.InitializeAsync();
        ThemeService.ThemeChanged += OnThemeChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await ThemeService.ApplyAsync();
            await WatchViewportAsync();
            StateHasChanged();
            return;
        }

        if (Module is null)
            return;

        if (RestoreFocusAfterRender)
        {
            RestoreFocusAfterRender = false;
            await Module.InvokeVoidAsync("restoreFocus", MainContent, NavigationHost);
        }

        if (!FocusDrawerAfterRender)
            return;

        FocusDrawerAfterRender = false;
        await Module.InvokeVoidAsync("focusFirst", NavigationHost);
    }

    /// <summary>
    /// Called from the browser when the viewport crosses the breakpoint between the persistent rail and the drawer.
    /// </summary>
    [JSInvokable]
    public Task OnCompactChanged(bool isCompact) => InvokeAsync(() =>
    {
        IsCompact = isCompact;
        IsDrawerOpen = false;
        StateHasChanged();
    });

    private async Task WatchViewportAsync()
    {
        Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Components/js/layout.js");

        SelfReference ??= DotNetObjectReference.Create(this);
        IsCompact = await Module.InvokeAsync<bool>("watchCompact", SelfReference);
    }

    private async Task ToggleNavigationAsync()
    {
        if (!IsCompact)
        {
            IsCollapsed = !IsCollapsed;
            return;
        }

        if (IsDrawerOpen)
        {
            await CloseDrawerWithFocusAsync();
            return;
        }

        IsDrawerOpen = true;
        FocusDrawerAfterRender = true;
    }

    private async Task CloseDrawerWithFocusAsync()
    {
        await CloseDrawerAsync();

        if (NavigationToggle?.Element is { } toggle)
            await toggle.FocusAsync();
    }

    private Task CloseDrawerAsync()
    {
        if (!IsDrawerOpen)
            return Task.CompletedTask;

        IsDrawerOpen = false;
        RestoreFocusAfterRender = true;
        return InvokeAsync(StateHasChanged);
    }

    private async Task OnNavigationKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key != "Escape" || !IsDrawerOpen)
            return;

        await CloseDrawerWithFocusAsync();
    }

    private async Task FocusMainAsync() => await MainContent.FocusAsync();

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (!IsDrawerOpen)
            return;

        _ = InvokeAsync(() =>
        {
            IsDrawerOpen = false;
            RestoreFocusAfterRender = true;
            StateHasChanged();
        });
    }

    private void OnThemeChanged() => InvokeAsync(StateHasChanged);

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public async ValueTask DisposeAsync()
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        LanguageService.LanguageChanged -= OnLanguageChanged;
        NavigationManager.LocationChanged -= OnLocationChanged;

        if (Module is not null)
        {
            try
            {
                await Module.InvokeVoidAsync("unwatchCompact");
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
