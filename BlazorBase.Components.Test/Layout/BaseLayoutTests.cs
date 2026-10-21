using BlazorBase.Components.Layout;
using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Xunit;

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
        Assert.Equal("base-navigation", cut.Find("fluent-button.base-nav-toggle").GetAttribute("aria-controls"));
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
        var toggle = cut.Find("fluent-button.base-nav-toggle");

        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        toggle.Click();

        Assert.Contains("collapsed", cut.Find("nav.base-side-nav").ClassList);
        Assert.Equal("false", cut.Find("fluent-button.base-nav-toggle").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void CompactViewport_ToggleOpensTheDrawerAndMakesTheContentInert()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();

        cut.Find("fluent-button.base-nav-toggle").Click();

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
        cut.Find("fluent-button.base-nav-toggle").Click();

        cut.Find(".base-nav-overlay").Click();

        Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList);
        Assert.False(cut.Find("main").HasAttribute("inert"));
    }

    [Fact]
    public void CompactViewport_EscapeClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find("fluent-button.base-nav-toggle").Click();

        cut.Find("aside#base-navigation").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList);
    }

    [Fact]
    public void CompactViewport_EscapeOutsideTheDrawerClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find("fluent-button.base-nav-toggle").Click();

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

        Assert.Contains("collapsed", cut.Find("nav.base-side-nav").ClassList);
        Assert.Equal("false", cut.Find("fluent-button.base-nav-toggle").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void WideViewport_TogglingRemembersTheCollapsedStateInTheBrowser()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.SetupVoid("writeCollapsed", true).SetVoidResult();
        module.SetupVoid("writeCollapsed", false).SetVoidResult();
        var cut = RenderWithNavigation();

        cut.Find("fluent-button.base-nav-toggle").Click();
        cut.Find("fluent-button.base-nav-toggle").Click();

        var written = module.Invocations["writeCollapsed"].Select(invocation => (bool)invocation.Arguments[0]!).ToList();
        Assert.Equal([true, false], written);
    }

    [Fact]
    public void CompactViewport_ClickingInsideTheNavigationClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find("fluent-button.base-nav-toggle").Click();

        cut.Find("nav.base-side-nav").Click();

        Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList);
    }

    [Fact]
    public void CompactViewport_NavigatingClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find("fluent-button.base-nav-toggle").Click();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/projects");

        cut.WaitForAssertion(() => Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList));
    }

    [Fact]
    public void LeavingTheCompactViewport_ClosesTheDrawer()
    {
        SimulateCompactViewport();
        var cut = RenderWithNavigation();
        cut.Find("fluent-button.base-nav-toggle").Click();

        cut.InvokeAsync(() => cut.Instance.OnCompactChanged(false));

        Assert.DoesNotContain("open", cut.Find("aside#base-navigation").ClassList);
        Assert.False(cut.Find("main").HasAttribute("inert"));
    }
}
