using AppTemplate.Client.Layout;
using AppTemplate.Shared.Modules.Authentication;
using AppTemplate.Tests.Infrastructure;
using BlazorBase.Components.Layout;
using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Services;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using IThemeService = BlazorBase.Components.Services.IThemeService;

namespace AppTemplate.Tests.Layout;

public class MainLayoutTests : BunitContext
{
    public MainLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazorBaseFluentUI();
        Services.AddLocalization();
        Services.AddSingleton<IThemeService, StubThemeService>();
        Services.AddSingleton<ILanguageService, StubLanguageService>();
        Services.AddBlazorBaseComponents();

        Authorization = this.AddAuthorization();
        Authorization.SetAuthorized("test-user");
    }

    private readonly BunitAuthorizationContext Authorization;

    [Fact]
    public void Render_AnonymousVisitor_SeesNoCommandPalette()
    {
        Authorization.SetNotAuthorized();

        var component = Render<MainLayout>();

        Assert.DoesNotContain("has-command-palette", component.Find("header").ClassList);
        Assert.Empty(component.FindAll("button.base-command-search"));
    }

    [Fact]
    public void Render_ShowsBrandLinkToTheAppRootWithAccessibleName()
    {
        var component = Render<MainLayout>();

        var brand = component.Find("a.app-brand");
        var localizer = Services.GetRequiredService<IStringLocalizer<AppBrand>>();
        Assert.Equal(localizer["HomeLabel"].Value, brand.GetAttribute("aria-label"));
        Assert.Equal("./", brand.GetAttribute("href"));
        Assert.Equal("icon.svg", brand.QuerySelector("img.app-brand-mark")?.GetAttribute("src"));
        Assert.Equal("AppTemplate", brand.QuerySelector(".app-brand-wordmark")?.TextContent);
    }

    [Fact]
    public void Render_ShowsBrandAtTheStartOfTheHeaderInsteadOfThePlainTitle()
    {
        var component = Render<MainLayout>();

        Assert.NotNull(component.Find(".base-header-start .base-header-brand a.app-brand"));
        Assert.Empty(component.FindAll(".base-header-title"));
        Assert.Contains("brand-start", component.Find("header").ClassList);
    }

    [Fact]
    public void Render_ShowsCommandPaletteSearchButtonInTheHeader()
    {
        var component = Render<MainLayout>();

        Assert.Contains("has-command-palette", component.Find("header").ClassList);
        Assert.NotNull(component.Find("header .base-header-search button.base-command-search"));
        Assert.NotEmpty(component.FindAll("header .base-command-search-icon"));
    }

    [Fact]
    public void Render_SignedIn_TurnsTheCommandPaletteOnAndPassesTheNavigationItems()
    {
        var component = Render<MainLayout>();

        var layout = component.FindComponent<BaseLayout>().Instance;
        Assert.True(layout.ShowCommandPalette);
        Assert.NotNull(layout.NavigationItems);
        Assert.NotEmpty(layout.NavigationItems);
    }

    [Fact]
    public void Render_UsesBrandPositionStartForTheBrand()
    {
        var component = Render<MainLayout>();

        Assert.Equal(BrandPosition.Start, component.FindComponent<BaseLayout>().Instance.BrandPosition);
    }

    [Fact]
    public void Render_ShowsTheThemeToggleInTheHeader()
    {
        var component = Render<MainLayout>();

        Assert.NotEmpty(component.FindComponents<BaseThemeToggle>());
    }

    [Fact]
    public void Render_NavigationIsAGroupedTreeSoTheSidebarHasHeadings()
    {
        var component = Render<MainLayout>();

        var navigationItems = component.FindComponent<BaseLayout>().Instance.NavigationItems!;
        Assert.NotEmpty(navigationItems);
        Assert.All(navigationItems, item =>
        {
            Assert.True(item.IsGroup, $"Top-level navigation entry '{item.Label}' must be a group with children.");
            Assert.NotNull(item.Key);
            Assert.True(item.ExpandedByDefault, $"Group '{item.Label}' must be expanded by default.");
        });
    }

    [Fact]
    public void Render_TheRailAndThePaletteShareOneTreeWithMobilePrimaryEntries()
    {
        var component = Render<MainLayout>();

        var navigationItems = component.FindComponent<BaseLayout>().Instance.NavigationItems!;
        var railItems = component.FindComponent<BaseSideNavigation>().Instance.Items;
        Assert.Same(navigationItems, railItems);
        Assert.Contains(navigationItems.SelectMany(group => group.Children), item => item.IsMobilePrimary);
    }

    [Fact]
    public void Render_Admin_ShowsBothGroupHeadingsInTheSidebar()
    {
        Authorization.SetRoles(RoleConstants.Admin);

        var component = Render<MainLayout>();

        var headings = component.FindComponents<FluentNavCategory>().Select(category => category.Instance.Title).ToList();
        var localizer = Services.GetRequiredService<IStringLocalizer<MainLayout>>();
        Assert.Equal([localizer["NavGroupWorkspace"].Value, localizer["NavGroupAdministration"].Value], headings);
        Assert.Contains("admin/users", component.Markup);
    }

    [Fact]
    public void Render_NonAdmin_ShowsTheWorkspaceHeadingAndHidesTheAdministrationGroup()
    {
        var component = Render<MainLayout>();

        var headings = component.FindComponents<FluentNavCategory>().Select(category => category.Instance.Title).ToList();
        var localizer = Services.GetRequiredService<IStringLocalizer<MainLayout>>();
        Assert.Equal([localizer["NavGroupWorkspace"].Value], headings);
        Assert.DoesNotContain("admin/users", component.Markup);
    }

    [Fact]
    public void Render_AnonymousVisitor_ShowsNoSidebarGroups()
    {
        Authorization.SetNotAuthorized();

        var component = Render<MainLayout>();

        Assert.Empty(component.FindComponents<FluentNavCategory>());
    }
}
