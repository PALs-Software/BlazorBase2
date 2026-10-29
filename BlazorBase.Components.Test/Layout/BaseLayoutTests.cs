using BlazorBase.Components.Layout;
using BlazorBase.Components.Layout.AppSwitcher;
using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;
using IThemeService = BlazorBase.Components.Services.IThemeService;

namespace BlazorBase.Components.Test.Layout;

public sealed class BaseLayoutTests : ComponentsBunitTestContextBase
{
    private readonly IThemeService ThemeService = Substitute.For<IThemeService>();
    private readonly ILanguageService LanguageService = Substitute.For<ILanguageService>();

    public BaseLayoutTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(ThemeService);
        Services.AddSingleton(LanguageService);
        AddAuthorization();
    }

    private void SimulateCompactViewport() =>
        JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js").Setup<bool>("watchCompact", _ => true).SetResult(true);

    private IRenderedComponent<BaseLayout> RenderWithNavigation()
    {
        var items = new List<NavigationItem> { new() { Href = "/projects", Label = "Projects" } };

        return Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.Navigation, builder =>
            {
                builder.OpenComponent<BaseSideNavigation>(0);
                builder.AddAttribute(1, nameof(BaseSideNavigation.Items), (IReadOnlyList<NavigationItem>)items);
                builder.CloseComponent();
            })
            .AddChildContent("<p id=\"page\">Page</p>"));
    }

    [Fact]
    public void ToastProvider_ShowsAtMostThreeToastsAtOnce()
    {
        var cut = Render<BaseLayout>(parameters => parameters.Add(p => p.AppTitle, "Shell"));

        Assert.NotNull(cut.FindComponent<FluentToastProvider>());
        Assert.Equal(3, Services.GetRequiredService<LibraryConfiguration>().Toast.MaxToastCount);
    }

    [Fact]
    public void FluentFieldsCarryNoDefaultVerticalMargin()
    {
        Assert.Null(Services.GetRequiredService<LibraryConfiguration>().DefaultStyles.FluentFieldClass);
    }

    [Fact]
    public void RendersMainLandmarkWithTheChildContent()
    {
        var cut = RenderWithNavigation();

        Assert.Equal("page", cut.Find("main#base-main #page").Id);
    }

    [Fact]
    public void RendersASkipLinkBeforeTheHeader()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        try
        {
            var cut = RenderWithNavigation();

            Assert.Equal("Skip to main content", cut.Find(".base-skip-link").TextContent);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void RendersTheNavigationInsideAnAsideAndOffersAToggle()
    {
        var cut = RenderWithNavigation();

        Assert.Single(cut.FindAll("aside#base-navigation a[href='/projects']"));
        Assert.Equal("base-navigation", cut.Find(".base-nav-toggle").GetAttribute("aria-controls"));
    }

    [Fact]
    public void WithoutNavigation_RendersNoAsideAndNoToggle()
    {
        var cut = Render<BaseLayout>(parameters => parameters.Add(p => p.AppTitle, "Shell"));

        Assert.Empty(cut.FindAll("aside"));
        Assert.Empty(cut.FindAll(".base-nav-toggle"));
    }

    [Fact]
    public void Brand_ReplacesTheTitleText()
    {
        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.Brand, "<img alt=\"logo\" />"));

        Assert.Single(cut.FindAll(".base-header-brand img"));
        Assert.Empty(cut.FindAll(".base-header-title"));
    }

    [Fact]
    public void WideViewport_ToggleCollapsesTheNavigationToIcons()
    {
        var cut = RenderWithNavigation();
        var toggle = cut.Find(".base-nav-toggle");

        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        toggle.Click();

        Assert.Contains("collapsed", cut.Find(".base-side-nav").ClassList);
        Assert.Equal("false", cut.Find(".base-nav-toggle").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void CompactViewport_ToggleOpensTheDrawerAndMakesTheContentInert()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();

        cut.Find(".base-nav-toggle").Click();

        Assert.Contains("open", cut.Find("aside#base-navigation").ClassList);
        Assert.Equal("dialog", cut.Find("aside#base-navigation").GetAttribute("role"));
        Assert.True(cut.Find("main").HasAttribute("inert"));
        Assert.Single(cut.FindAll(".base-nav-overlay"));
    }

    [Fact]
    public void CompactViewport_ClickingTheOverlayClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find(".base-nav-toggle").Click();

        cut.Find(".base-nav-overlay").Click();

        Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList);
        Assert.False(cut.Find("main").HasAttribute("inert"));
    }

    [Fact]
    public void CompactViewport_EscapeClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find(".base-nav-toggle").Click();

        cut.Find("aside#base-navigation").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList);
    }

    [Fact]
    public void CompactViewport_EscapeOutsideTheDrawerClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find(".base-nav-toggle").Click();

        cut.InvokeAsync(() => cut.Instance.OnEscapeOutsideDrawer());

        Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList);
        Assert.False(cut.Find("main").HasAttribute("inert"));
    }

    [Fact]
    public void AppliesTheThemeOnceBeforeTheFirstContentIsShown()
    {
        var cut = RenderWithNavigation();

        _ = ThemeService.Received(1).ApplyAsync();
        Assert.Equal("page", cut.Find("main#base-main #page").Id);
    }

    [Fact]
    public void WideViewport_RestoresTheCollapsedStateRememberedByTheBrowser()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.Setup<bool>("readCollapsed").SetResult(true);

        var cut = RenderWithNavigation();

        Assert.Contains("collapsed", cut.Find(".base-side-nav").ClassList);
        Assert.Equal("false", cut.Find(".base-nav-toggle").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void WideViewport_TogglingRemembersTheCollapsedStateInTheBrowser()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.SetupVoid("writeCollapsed", true).SetVoidResult();
        module.SetupVoid("writeCollapsed", false).SetVoidResult();
        var cut = RenderWithNavigation();

        cut.Find(".base-nav-toggle").Click();
        cut.Find(".base-nav-toggle").Click();

        var written = module.Invocations["writeCollapsed"].Select(invocation => (bool)invocation.Arguments[0]!).ToList();
        Assert.Equal([true, false], written);
    }

    [Fact]
    public void CompactViewport_ClickingAnEntryInTheNavigationClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find(".base-nav-toggle").Click();

        cut.Find("a[href='/projects']").Click();

        Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList);
    }

    [Fact]
    public void CompactViewport_NavigatingClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find(".base-nav-toggle").Click();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/projects");

        cut.WaitForAssertion(() => Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList));
    }

    [Fact]
    public void LeavingTheCompactViewport_ClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find(".base-nav-toggle").Click();

        cut.InvokeAsync(() => cut.Instance.OnCompactChanged(false));

        Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList);
        Assert.False(cut.Find("main").HasAttribute("inert"));
    }

    private void SimulatePhoneViewport(bool alsoCompact = false)
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.Setup<bool>("watchPhone", _ => true).SetResult(true);

        if (alsoCompact)
            module.Setup<bool>("watchCompact", _ => true).SetResult(true);
    }

    private static IReadOnlyList<NavigationItem> QuickLinks() =>
    [
        new() { Href = "/", Label = "Home", MatchAll = true, IsMobilePrimary = true },
        new() { Href = "/projects", Label = "Projects", IsMobilePrimary = true },
    ];

    private IRenderedComponent<BaseLayout> RenderWithBottomBar(IReadOnlyList<NavigationItem>? items)
    {
        var navigationItems = new List<NavigationItem> { new() { Href = "/projects", Label = "Projects" } };

        return Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.BottomNavigationItems, items)
            .Add(p => p.Navigation, builder =>
            {
                builder.OpenComponent<BaseSideNavigation>(0);
                builder.AddAttribute(1, nameof(BaseSideNavigation.Items), (IReadOnlyList<NavigationItem>)navigationItems);
                builder.CloseComponent();
            }));
    }

    [Fact]
    public void Brand_IsCenteredByDefault_AndCanStayAtTheStart()
    {
        var centered = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.Brand, "<img alt=\"logo\" />"));
        var atStart = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.BrandPosition, BrandPosition.Start)
            .Add(p => p.Brand, "<img alt=\"logo\" />"));

        Assert.Contains("brand-centered", centered.Find("header").ClassList);
        Assert.Single(centered.FindAll(".base-header-center .base-header-brand"));
        Assert.Contains("brand-start", atStart.Find("header").ClassList);
        Assert.Empty(atStart.FindAll(".base-header-center"));
        Assert.Single(atStart.FindAll(".base-header-start .base-header-brand"));
    }

    [Fact]
    public void AppSwitcher_RendersRightAfterTheNavigationButton_AndReplacesTheBrandAndTheTitle()
    {
        var items = new List<NavigationItem> { new() { Href = "/projects", Label = "Projects" } };

        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.Brand, "<img alt=\"logo\" />")
            .Add(p => p.AppSwitcher, "<span id=\"switcher\">Switcher</span>")
            .Add(p => p.Navigation, builder =>
            {
                builder.OpenComponent<BaseSideNavigation>(0);
                builder.AddAttribute(1, nameof(BaseSideNavigation.Items), (IReadOnlyList<NavigationItem>)items);
                builder.CloseComponent();
            }));

        var start = cut.Find(".base-header-start");
        Assert.Contains("base-nav-toggle", start.Children[0].ClassList);
        Assert.Equal("switcher", start.Children[1].Id);
        Assert.Empty(cut.FindAll(".base-header-brand"));
        Assert.Empty(cut.FindAll(".base-header-title"));
        Assert.Empty(cut.FindAll(".base-header-center"));
        Assert.Contains("brand-start", cut.Find("header").ClassList);
        Assert.DoesNotContain("brand-centered", cut.Find("header").ClassList);
    }

    [Fact]
    public void AppSwitcher_LeftAlignsTheHeaderEvenWithACenteredBrandPosition()
    {
        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.BrandPosition, BrandPosition.Center)
            .Add(p => p.AppSwitcher, "<span id=\"switcher\">Switcher</span>"));

        Assert.Single(cut.FindAll(".base-header-start #switcher"));
        Assert.Empty(cut.FindAll(".base-header-center"));
    }

    [Fact]
    public void WithoutAnAppSwitcher_TheBrandStaysWhereItWas()
    {
        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.Brand, "<img alt=\"logo\" />"));

        Assert.Single(cut.FindAll(".base-header-center .base-header-brand"));
        Assert.Empty(cut.FindAll(".base-app-switcher"));
    }

    [Fact]
    public void AppSwitcherComponent_RendersItsChipInTheHeader()
    {
        var groups = new List<AppLinkGroup>
        {
            new() { Key = "g", Label = "G", Links = [new() { Key = "dev", Label = "dev", Href = "https://dev.example/" }] },
        };

        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.AppSwitcher, builder =>
            {
                builder.OpenComponent<BaseAppSwitcher>(0);
                builder.AddAttribute(1, nameof(BaseAppSwitcher.Groups), (IReadOnlyList<AppLinkGroup>)groups);
                builder.AddAttribute(2, nameof(BaseAppSwitcher.CurrentKey), "dev");
                builder.CloseComponent();
            }));

        Assert.Equal("dev", cut.Find(".base-header-start .base-app-switcher-chip-label").TextContent);
    }

    [Fact]
    public void Header_OffersTheThemeToggleAndTheSettingsButton_ButNoLanguagePicker()
    {
        var cut = RenderWithNavigation();

        Assert.Single(cut.FindAll(".base-header-theme"));
        Assert.Equal("dialog", cut.Find(".base-settings-toggle").GetAttribute("aria-haspopup"));
        Assert.Empty(cut.FindAll("header .base-language-picker"));
        Assert.Empty(cut.FindComponents<FluentSelect<string, string>>());
    }

    [Fact]
    public void ShowSettingsAndThemeToggle_CanBeSwitchedOff()
    {
        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.ShowSettings, false)
            .Add(p => p.ShowThemeToggle, false));

        Assert.Empty(cut.FindAll(".base-settings-toggle"));
        Assert.Empty(cut.FindAll(".base-header-theme"));
    }

    [Fact]
    public void SettingsButton_OpensThePanelWithHostSections_AndClosesItAgain()
    {
        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.SettingsContent, context => builder =>
            {
                builder.OpenElement(0, "p");
                builder.AddAttribute(1, "id", "host-section");
                builder.CloseElement();
            })
            .AddChildContent("<p id=\"page\">Page</p>"));
        Assert.Empty(cut.FindAll(".base-settings-panel"));

        cut.Find(".base-settings-toggle").Click();

        Assert.Single(cut.FindAll(".base-settings-panel #host-section"));
        Assert.Equal("true", cut.Find(".base-settings-toggle").GetAttribute("aria-expanded"));
        Assert.True(cut.Find("main").HasAttribute("inert"));

        cut.Find(".base-settings-close").Click();

        Assert.Empty(cut.FindAll(".base-settings-panel"));
        Assert.False(cut.Find("main").HasAttribute("inert"));
    }

    [Fact]
    public void NavigatingWhileTheSettingsPanelIsOpen_ClosesIt()
    {
        var cut = RenderWithNavigation();
        cut.Find(".base-settings-toggle").Click();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/projects");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-settings-panel")));
    }

    [Fact]
    public async Task LanguageChosenInThePanel_IsHandedToTheHost()
    {
        var chosen = new List<string>();
        LanguageService.CurrentLanguage.Returns("en");
        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.OnLanguageSelected, EventCallback.Factory.Create<string>(this, code => chosen.Add(code))));
        cut.Find(".base-settings-toggle").Click();

        await cut.InvokeAsync(() => cut.FindComponents<FluentRadioGroup<string>>()[0].Instance.ValueChanged.InvokeAsync("de"));

        Assert.Equal(["de"], chosen);
    }

    [Fact]
    public void PhoneViewport_ShowsTheBottomBarWithQuickLinksAndAMoreTab()
    {
        SimulatePhoneViewport();

        var cut = RenderWithBottomBar(QuickLinks());

        Assert.Contains("has-bottom-bar", cut.Find(".layout-root").ClassList);
        Assert.Equal(2, cut.FindAll(".base-bottom-host a.base-bottom-tab").Count);
        Assert.Single(cut.FindAll(".base-bottom-host button.base-bottom-more"));
    }

    [Fact]
    public void WideViewport_HasNoBottomBar()
    {
        var cut = RenderWithBottomBar(QuickLinks());

        Assert.Empty(cut.FindAll(".base-bottom-host"));
        Assert.DoesNotContain("has-bottom-bar", cut.Find(".layout-root").ClassList);
    }

    [Fact]
    public void WithoutBottomNavigationItems_PhoneGetsNoBottomBar()
    {
        SimulatePhoneViewport();

        var cut = RenderWithBottomBar(null);

        Assert.Empty(cut.FindAll(".base-bottom-host"));
    }

    [Fact]
    public void MoreTab_OpensTheNavigationDrawer()
    {
        SimulatePhoneViewport(alsoCompact: true);
        var cut = RenderWithBottomBar(QuickLinks());

        cut.Find("button.base-bottom-more").Click();

        Assert.Contains("open", cut.Find("aside#base-navigation").ClassList);
        Assert.Equal("true", cut.Find("button.base-bottom-more").GetAttribute("aria-expanded"));
        Assert.Equal("base-navigation", cut.Find("button.base-bottom-more").GetAttribute("aria-controls"));
        Assert.Empty(cut.FindAll(".base-more-sheet"));
    }

    [Fact]
    public void LeavingThePhoneViewport_RemovesTheBottomBar()
    {
        SimulatePhoneViewport();
        var cut = RenderWithBottomBar(QuickLinks());

        cut.InvokeAsync(() => cut.Instance.OnPhoneChanged(false));

        Assert.Empty(cut.FindAll(".base-bottom-host"));
    }

    private IRenderedComponent<BaseLayout> RenderWithSharedNavigation(IReadOnlyList<NavigationItem> items, int maxBottomBarItems = 4) =>
        Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.BottomNavigationItems, items)
            .Add(p => p.MaxBottomBarItems, maxBottomBarItems)
            .Add(p => p.Navigation, builder =>
            {
                builder.OpenComponent<BaseSideNavigation>(0);
                builder.AddAttribute(1, nameof(BaseSideNavigation.Items), items);
                builder.CloseComponent();
            }));

    private static IReadOnlyList<NavigationItem> QuickLinksWithBottomBarOnlyAlerts() =>
    [
        new() { Href = "/", Label = "Home", MatchAll = true, IsMobilePrimary = true },
        new() { Href = "/alerts", Label = "Alerts", IsMobilePrimary = true, BottomBarOnly = true, BottomBarOrder = 1 },
    ];

    [Fact]
    public void BottomBarOnlyTab_IsInTheBarOnAPhone_ButNotInTheDrawerList()
    {
        SimulatePhoneViewport(alsoCompact: true);

        var cut = RenderWithSharedNavigation(QuickLinksWithBottomBarOnlyAlerts());

        Assert.Single(cut.FindAll(".base-bottom-host a[href=\"/alerts\"]"));
        Assert.Empty(cut.FindAll("aside#base-navigation a[href=\"/alerts\"]"));
    }

    [Fact]
    public void BottomBarOnlyItem_BeyondTheBarCapacity_IsListedInTheDrawerOnAPhone()
    {
        SimulatePhoneViewport(alsoCompact: true);

        var cut = RenderWithSharedNavigation(QuickLinksWithBottomBarOnlyAlerts(), maxBottomBarItems: 1);

        Assert.Empty(cut.FindAll(".base-bottom-host a[href=\"/alerts\"]"));
        Assert.Single(cut.FindAll("aside#base-navigation a[href=\"/alerts\"]"));
    }

    [Fact]
    public void BottomBarOnlyItem_IsNotListedInTheSidebarOnWideScreens()
    {
        var cut = RenderWithSharedNavigation(QuickLinksWithBottomBarOnlyAlerts());

        Assert.Empty(cut.FindAll("a[href=\"/alerts\"]"));
        Assert.Single(cut.FindAll("aside#base-navigation a[href=\"/\"]"));
    }
}
