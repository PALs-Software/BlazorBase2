using BlazorBase.Components.CommandPalette;
using BlazorBase.Components.Help;
using BlazorBase.Components.Layout.AppSwitcher;
using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Layout;

/// <summary>
/// The application shell: a header, a navigation area and the main content.
/// </summary>
/// <remarks>
/// Above 1024 px the navigation is a persistent rail that the header button reduces to icons; the choice is
/// remembered in the browser. At 1024 px
/// and below it becomes an off-canvas drawer opened by the same button, closed again by navigating,
/// Escape or a click on the overlay; focus moves into the drawer while it is open, Tab cycles through the
/// drawer and its button instead of reaching the page behind it, and focus returns to the
/// button afterwards. The shell fills the dynamic viewport and only the main content scrolls.
/// Header content can mark elements with <c>base-header-wide-only</c> to hide them at 768 px and below,
/// and navigation content can mark elements with <c>base-nav-narrow-only</c> to show them only in the
/// drawer at that width — the place to move controls that do not fit a phone header.
/// The brand is centered in the header by default (<see cref="BrandPosition"/>). A settings button at the end of
/// the header opens a <see cref="BaseSettingsPanel"/> with the language and theme plus the host's own sections
/// (<see cref="SettingsContent"/>), next to a quick theme toggle. Given <see cref="BottomNavigationItems"/>, a phone
/// (768 px and below) also gets a bottom bar with quick links and a "More" tab that opens the drawer; the bar is a
/// row of the shell, so the content never runs behind it, and it steps aside while the on-screen keyboard is open.
/// With <see cref="ShowHelp"/> a help button left of the settings button opens the <see cref="BaseHelpPanel"/>;
/// opening the help closes the settings panel and the navigation drawer, and the other way round.
/// An <see cref="AppSwitcher"/> chip next to the navigation button replaces the brand and the title and
/// left-aligns the header. With <see cref="ShowCommandPalette"/> a search field takes the center of the header (a
/// search button on a phone), the brand moves next to the navigation button, and Ctrl+K opens the
/// <see cref="BaseCommandPalette"/>.
/// </remarks>
public partial class BaseLayout(
    IThemeService themeService,
    ILanguageService languageService,
    IJSRuntime jsRuntime,
    NavigationManager navigationManager,
    IStringLocalizer<BaseLayout> localizer,
    IServiceProvider serviceProvider) : ComponentBase, IAsyncDisposable
{
    #region Injects

    private readonly IThemeService ThemeService = themeService;
    private readonly ILanguageService LanguageService = languageService;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IStringLocalizer<BaseLayout> Localizer = localizer;
    private readonly IServiceProvider ServiceProvider = serviceProvider;

    #endregion

    [Parameter]
    public string AppTitle { get; set; } = string.Empty;

    /// <summary>Replaces the plain <see cref="AppTitle"/> text in the header, e.g. with a logo.</summary>
    [Parameter]
    public RenderFragment? Brand { get; set; }

    /// <summary>
    /// Where the brand (or the title) sits in the header: centered between the navigation button and the actions,
    /// or at its start next to the button.
    /// </summary>
    [Parameter]
    public BrandPosition BrandPosition { get; set; } = BrandPosition.Center;

    /// <summary>
    /// The website switcher, usually a <see cref="BlazorBase.Components.Layout.AppSwitcher.BaseAppSwitcher"/>. It is rendered right after the
    /// navigation button at the start of the header and replaces the brand and the title, which are then not
    /// rendered; the header content is left aligned. Without it the header behaves as before.
    /// </summary>
    [Parameter]
    public RenderFragment? AppSwitcher { get; set; }

    /// <summary>
    /// The groups of links to other apps and websites, passed once for everything that uses them. Given
    /// links, the header shows a default <see cref="BlazorBase.Components.Layout.AppSwitcher.BaseAppSwitcher"/> when
    /// <see cref="AppSwitcher"/> is not set; a <c>BaseAppSwitcher</c> placed in <see cref="AppSwitcher"/> without
    /// groups of its own shows them too, and the command palette offers them under "Apps".
    /// </summary>
    [Parameter]
    public IReadOnlyList<AppLinkGroup>? AppLinks { get; set; }

    /// <summary>The <see cref="AppLink.Key"/> of the app the user is in, for the app switcher chip.</summary>
    [Parameter]
    public string? CurrentAppKey { get; set; }

    /// <summary>
    /// Shows the search field in the header (a search button on a phone) and the <see cref="BaseCommandPalette"/> it
    /// opens, which Ctrl+K opens from anywhere. The brand then moves next to the navigation button. Needs the services
    /// of <c>AddBlazorBaseComponents</c>; the palette offers <see cref="NavigationItems"/> (else
    /// <see cref="BottomNavigationItems"/>) as pages and <see cref="AppLinks"/> as apps, plus every registered
    /// <see cref="ICommandPaletteProvider"/>. Without it the header is unchanged.
    /// </summary>
    [Parameter]
    public bool ShowCommandPalette { get; set; }

    /// <summary>
    /// The application's navigation entries, normally the list the side navigation shows. The command palette lists
    /// them as pages; it uses <see cref="BottomNavigationItems"/> when this is not set, so a host that already passes
    /// the list for the bottom bar passes nothing more. Unlike <see cref="BottomNavigationItems"/> it does not show a
    /// bottom bar.
    /// </summary>
    [Parameter]
    public IReadOnlyList<NavigationItem>? NavigationItems { get; set; }

    [Parameter]
    public RenderFragment? Navigation { get; set; }

    [Parameter]
    public RenderFragment? Banner { get; set; }

    [Parameter]
    public RenderFragment? HeaderContent { get; set; }

    /// <summary>Controls rendered after the built-in buttons, at the very end of the header (e.g. sign out).</summary>
    [Parameter]
    public RenderFragment? HeaderEndContent { get; set; }

    /// <summary>Shows the quick theme toggle in the header, in front of the settings button.</summary>
    [Parameter]
    public bool ShowThemeToggle { get; set; } = true;

    /// <summary>Shows the settings button in the header and the <see cref="BaseSettingsPanel"/> it opens.</summary>
    [Parameter]
    public bool ShowSettings { get; set; } = true;

    /// <summary>
    /// Shows a help button ("?") left of the settings button and the <see cref="BaseHelpPanel"/> it opens, which
    /// shows the help topic of the current page. Needs the services of <c>AddBlazorBaseHelp</c>. The panel sits next to
    /// the page from 1200 px up and becomes a modal sheet below.
    /// </summary>
    [Parameter]
    public bool ShowHelp { get; set; }

    /// <summary>Sections the host adds to the settings panel, below the language and the theme.</summary>
    [Parameter]
    public RenderFragment<SettingsPanelContext>? SettingsContent { get; set; }

    /// <summary>The languages the settings panel offers; null offers English and German.</summary>
    [Parameter]
    public IReadOnlyList<KeyValuePair<string, string>>? LanguageOptions { get; set; }

    /// <summary>
    /// Raised with the language code chosen in the settings panel instead of changing the language directly, so the
    /// host can store the choice before it calls <see cref="ILanguageService.SetLanguageAsync"/> itself.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnLanguageSelected { get; set; }

    /// <summary>
    /// The navigation entries a phone's bottom bar draws its quick links from — normally the list the side
    /// navigation shows. The bar appears at 768 px and below and is left out while this is null or empty.
    /// </summary>
    [Parameter]
    public IReadOnlyList<NavigationItem>? BottomNavigationItems { get; set; }

    /// <summary>The most quick links in the bottom bar, not counting its "More" tab.</summary>
    [Parameter]
    public int MaxBottomBarItems { get; set; } = 4;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private IJSObjectReference? Module;
    private DotNetObjectReference<BaseLayout>? SelfReference;
    private FluentButton? NavigationToggle;
    private FluentButton? SettingsToggle;
    private FluentButton? HelpToggle;
    private ElementReference NavigationHost;
    private ElementReference MainContent;
    private bool IsCompact;
    private bool IsPhone;
    private bool IsWide;
    private bool IsDrawerOpen;
    private bool IsSettingsOpen;
    private bool IsHelpOpen;
    private string? HelpAnchorTopic;
    private string? HelpAnchor;
    private int HelpAnchorRequest;
    private bool IsCollapsed;
    private bool BrowserStateApplied;
    private bool FocusDrawerAfterRender;
    private bool RestoreFocusAfterRender;

    private const string SettingsPanelId = "base-settings";
    private const string HelpPanelId = "base-help";

    private static readonly RenderFragment DefaultAppSwitcher = builder =>
    {
        builder.OpenComponent<BaseAppSwitcher>(0);
        builder.CloseComponent();
    };

    private AppSwitcherContext SwitcherContext = new([], null);
    private IReadOnlyList<ICommandPaletteProvider> PaletteProviders = [];
    private IReadOnlyList<NavigationItem>? PaletteNavigationSource;
    private IReadOnlyList<AppLinkGroup>? PaletteLinksSource;
    private bool PaletteProvidersBuilt;

    private bool HasAppSwitcher => AppSwitcher is not null || AppLinks is { Count: > 0 };

    private bool IsBrandCentered => !HasAppSwitcher && !ShowCommandPalette && BrandPosition == BrandPosition.Center;

    private bool IsHelpModal => ShowHelp && IsHelpOpen && !IsWide;

    private bool AreFieldTooltipsEnabled =>
        ShowHelp
        && ServiceProvider.GetService<IOptions<HelpOptions>>()?.Value.ShowFieldTooltips == true
        && ServiceProvider.GetService<IHelpFieldIndex>() is not null;

    private bool HasBottomBar => IsPhone && BottomNavigationItems is { Count: > 0 };

    private EventCallback MoreCallback => Navigation is null
        ? default
        : EventCallback.Factory.Create(this, ToggleNavigationAsync);

    private DesignThemeModes CurrentMode => ThemeService.CurrentTheme switch
    {
        ThemePreference.Light => DesignThemeModes.Light,
        ThemePreference.Dark => DesignThemeModes.Dark,
        _ => DesignThemeModes.System,
    };

    private NavigationLayoutContext NavigationContext => new(IsCollapsed && !IsCompact, CloseDrawerAsync, HasBottomBar ? MaxBottomBarItems : null);

    private bool IsMainInert => (IsDrawerOpen && IsCompact) || IsSettingsOpen || IsHelpModal;

    private string NavigationExpandedValue => (IsCompact ? IsDrawerOpen : !IsCollapsed) ? "true" : "false";

    private string NavigationToggleLabel => (IsCompact, IsDrawerOpen, IsCollapsed) switch
    {
        (true, true, _) => Localizer["CloseNavigation"],
        (true, false, _) => Localizer["OpenNavigation"],
        (false, _, true) => Localizer["ExpandNavigation"],
        _ => Localizer["CollapseNavigation"],
    };

    protected override void OnParametersSet()
    {
        UpdateSwitcherContext();
        UpdatePaletteProviders();
    }

    private void UpdateSwitcherContext()
    {
        var groups = AppLinks ?? Array.Empty<AppLinkGroup>();
        if (ReferenceEquals(SwitcherContext.Groups, groups) && SwitcherContext.CurrentKey == CurrentAppKey)
            return;

        SwitcherContext = new AppSwitcherContext(groups, CurrentAppKey);
    }

    private void UpdatePaletteProviders()
    {
        if (!ShowCommandPalette)
        {
            PaletteProviders = [];
            PaletteNavigationSource = null;
            PaletteLinksSource = null;
            PaletteProvidersBuilt = false;
            return;
        }

        var navigation = NavigationItems ?? BottomNavigationItems;
        if (PaletteProvidersBuilt && ReferenceEquals(navigation, PaletteNavigationSource) && ReferenceEquals(AppLinks, PaletteLinksSource))
            return;

        var providerLocalizer = ServiceProvider.GetRequiredService<IStringLocalizer<BaseCommandPalette>>();
        var providers = new List<ICommandPaletteProvider>();
        if (navigation is { Count: > 0 })
            providers.Add(new NavigationCommandProvider(navigation, providerLocalizer));

        if (AppLinks is { Count: > 0 })
            providers.Add(new AppLinkCommandProvider(AppLinks, providerLocalizer));

        PaletteProviders = providers;
        PaletteNavigationSource = navigation;
        PaletteLinksSource = AppLinks;
        PaletteProvidersBuilt = true;
    }

    private Task OpenCommandPaletteAsync() =>
        ServiceProvider.GetRequiredService<ICommandPaletteService>().OpenAsync();

    protected override async Task OnInitializedAsync()
    {
        await ThemeService.InitializeAsync();
        await LanguageService.InitializeAsync();
        ThemeService.ThemeChanged += OnThemeChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        NavigationManager.LocationChanged += OnLocationChanged;

        try
        {
            await ApplyBrowserStateAsync();
        }
        catch (InvalidOperationException)
        {
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await ApplyBrowserStateAsync();
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

    /// <summary>
    /// Called from the browser when the viewport crosses the phone breakpoint, where the bottom bar takes over.
    /// </summary>
    [JSInvokable]
    public Task OnPhoneChanged(bool isPhone) => InvokeAsync(() =>
    {
        IsPhone = isPhone;
        StateHasChanged();
    });

    /// <summary>
    /// Called from the browser when the viewport crosses the width from which the help panel sits next to the page.
    /// </summary>
    [JSInvokable]
    public Task OnWideChanged(bool isWide) => InvokeAsync(() =>
    {
        IsWide = isWide;
        StateHasChanged();
    });

    /// <summary>
    /// Called from the browser when Escape is pressed while the drawer is open and the focus is outside it.
    /// </summary>
    [JSInvokable]
    public Task OnEscapeOutsideDrawer() => InvokeAsync(CloseDrawerWithFocusAsync);

    /// <summary>
    /// Applies what only the browser knows before the first content is shown: the theme, so the account preference
    /// does not appear after a flash of the wrong one, and the remembered collapsed state of the navigation rail.
    /// </summary>
    /// <remarks>
    /// Runs from <c>OnInitializedAsync</c> so the first render already carries both. Where JavaScript is not yet
    /// available (prerendering) the call throws and the first-render pass runs it instead.
    /// </remarks>
    private async Task ApplyBrowserStateAsync()
    {
        if (BrowserStateApplied)
            return;

        await ThemeService.ApplyAsync();
        var module = await LoadModuleAsync();
        IsCollapsed = await module.InvokeAsync<bool>("readCollapsed");
        BrowserStateApplied = true;
    }

    private async Task<IJSObjectReference> LoadModuleAsync() =>
        Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Components/js/layout.js");

    private async Task WatchViewportAsync()
    {
        var module = await LoadModuleAsync();
        SelfReference ??= DotNetObjectReference.Create(this);
        IsCompact = await module.InvokeAsync<bool>("watchCompact", SelfReference);
        IsPhone = await module.InvokeAsync<bool>("watchPhone", SelfReference);
        IsWide = await module.InvokeAsync<bool>("watchWide", SelfReference);
    }

    private async Task OpenSettingsAsync()
    {
        IsHelpOpen = false;

        if (IsDrawerOpen)
            await CloseDrawerAsync();

        IsSettingsOpen = true;
    }

    private async Task ToggleHelpAsync()
    {
        if (IsHelpOpen)
        {
            await CloseHelpAsync();
            return;
        }

        IsSettingsOpen = false;

        if (IsDrawerOpen)
            await CloseDrawerAsync();

        IsHelpOpen = true;
    }

    private async Task OpenHelpAtAsync(HelpAnchorTarget target)
    {
        if (!ShowHelp)
            return;

        HelpAnchorTopic = target.Topic;
        HelpAnchor = target.Anchor;
        HelpAnchorRequest++;

        if (IsHelpOpen)
            return;

        IsSettingsOpen = false;

        if (IsDrawerOpen)
            await CloseDrawerAsync();

        IsHelpOpen = true;
    }

    private async Task CloseHelpAsync()
    {
        IsHelpOpen = false;

        if (HelpToggle?.Element is { } toggle)
            await toggle.FocusAsync();
    }

    private async Task CloseSettingsAsync()
    {
        IsSettingsOpen = false;

        if (SettingsToggle?.Element is { } toggle)
            await toggle.FocusAsync();
    }

    private async Task ToggleNavigationAsync()
    {
        if (!IsCompact)
        {
            IsCollapsed = !IsCollapsed;

            if (Module is not null)
                await Module.InvokeVoidAsync("writeCollapsed", IsCollapsed);

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
        if (!IsDrawerOpen && !IsSettingsOpen)
            return;

        _ = InvokeAsync(() =>
        {
            IsDrawerOpen = false;
            IsSettingsOpen = false;
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
                await Module.InvokeVoidAsync("unwatchPhone");
                await Module.InvokeVoidAsync("unwatchWide");
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
