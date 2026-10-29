using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Components.Test.Layout;

public sealed class BaseSideNavigationTests : ComponentsBunitTestContextBase
{
    private static IReadOnlyList<NavigationItem> SampleItems() =>
    [
        new() { Href = "/", MatchAll = true, Label = "Home" },
        new() { Href = "/projects", Label = "Projects" },
        new()
        {
            Label = "Admin",
            RequiredRole = "Admin",
            Children =
            [
                new() { Href = "/admin/users", Label = "Users" },
            ],
        },
    ];

    private IRenderedComponent<BaseSideNavigation> Render(IReadOnlyList<NavigationItem> items)
    {
        Services.AddLocalization();
        return Render<BaseSideNavigation>(parameters => parameters.Add(p => p.Items, items));
    }

    [Fact]
    public void RendersAllPlainLinks()
    {
        var cut = Render(SampleItems());

        Assert.Contains("href=\"/\"", cut.Markup);
        Assert.Contains("href=\"/projects\"", cut.Markup);
    }

    [Fact]
    public void HidesRoleGatedGroup_ForNonAdmin()
    {
        AddAuthorization().SetAuthorized("user");

        var cut = Render(SampleItems());

        Assert.DoesNotContain("href=\"/admin/users\"", cut.Markup);
        Assert.Empty(cut.FindComponents<FluentNavCategory>());
    }

    [Fact]
    public void ShowsRoleGatedGroupAsSection_ForAdmin()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");

        var cut = Render(SampleItems());

        Assert.Equal("Admin", Assert.Single(cut.FindComponents<FluentNavCategory>()).Instance.Title);
        Assert.Contains("href=\"/admin/users\"", cut.Markup);
    }

    [Fact]
    public void CollapsedLayoutContext_ShowsIconsOnlyWithoutSectionLabels()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");
        Services.AddLocalization();

        var cut = Render<BaseSideNavigation>(parameters => parameters
            .Add(p => p.Items, SampleItems())
            .AddCascadingValue(new NavigationLayoutContext(true, () => Task.CompletedTask)));

        Assert.Contains("collapsed", cut.Find(".base-side-nav").ClassList);
        Assert.Empty(cut.FindComponents<FluentNavCategory>());
        Assert.DoesNotContain("base-side-nav-group-label", cut.Markup);
    }

    [Fact]
    public void ClickingAnEntry_AsksTheLayoutToCloseTheDrawer()
    {
        var closed = 0;
        Services.AddLocalization();

        var cut = Render<BaseSideNavigation>(parameters => parameters
            .Add(p => p.Items, SampleItems())
            .AddCascadingValue(new NavigationLayoutContext(false, () =>
            {
                closed++;
                return Task.CompletedTask;
            })));

        cut.Find("a[href='/projects']").Click();

        Assert.Equal(1, closed);
    }

    private static IReadOnlyList<NavigationItem> GroupedItems() =>
    [
        new() { Href = "/", MatchAll = true, Label = "Home" },
        new()
        {
            Label = "Administration",
            Key = "administration",
            Children =
            [
                new() { Href = "/admin/users", Label = "Users" },
                new() { Href = "/admin/tokens", Label = "Tokens" },
            ],
        },
        new()
        {
            Label = "Telemetry",
            Key = "telemetry",
            Children = [new() { Href = "/telemetry/overview", Label = "Overview" }],
        },
    ];

    private IRenderedComponent<BaseSideNavigation> RenderGroups(bool collapsed = false) => Render<BaseSideNavigation>(parameters => parameters
            .Add(p => p.Items, GroupedItems())
            .AddCascadingValue(new NavigationLayoutContext(collapsed, () => Task.CompletedTask)));

    [Fact]
    public void Groups_StartCollapsed_WithTheirLabelAndAnExpandedStateOnTheCategory()
    {
        var cut = RenderGroups();

        var category = cut.FindComponents<FluentNavCategory>()[0];

        Assert.Equal("Administration", category.Instance.Title);
        Assert.False(category.Instance.Expanded);
        Assert.Equal("false", category.Find("button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void ClickingAGroup_ExpandsItAndRemembersTheChoiceInTheBrowser()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.SetupVoid("writeGroupState", _ => true).SetVoidResult();
        var cut = RenderGroups();

        cut.FindComponents<FluentNavCategory>()[0].Find("button").Click();

        Assert.True(cut.FindComponents<FluentNavCategory>()[0].Instance.Expanded);
        var written = module.Invocations["writeGroupState"].Single();
        Assert.Equal("administration", written.Arguments[0]);
        Assert.Equal(true, written.Arguments[1]);
    }

    [Fact]
    public void ClickingAGroup_DoesNotAskTheLayoutToCloseTheDrawer()
    {
        var closed = 0;
        var cut = Render<BaseSideNavigation>(parameters => parameters
            .Add(p => p.Items, GroupedItems())
            .AddCascadingValue(new NavigationLayoutContext(false, () =>
            {
                closed++;
                return Task.CompletedTask;
            })));

        cut.FindComponents<FluentNavCategory>()[0].Find("button").Click();

        Assert.Equal(0, closed);
    }

    [Fact]
    public void RememberedStates_AreAppliedBeforeTheFirstRender()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.Setup<Dictionary<string, bool>?>("readGroupStates", _ => true)
            .SetResult(new Dictionary<string, bool> { ["telemetry"] = true });

        var cut = RenderGroups();

        var categories = cut.FindComponents<FluentNavCategory>();
        Assert.False(categories[0].Instance.Expanded);
        Assert.True(categories[1].Instance.Expanded);
    }

    [Fact]
    public void TheGroupHoldingTheCurrentPage_IsExpandedAndMarked_EvenWhenRememberedAsCollapsed()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.Setup<Dictionary<string, bool>?>("readGroupStates", _ => true)
            .SetResult(new Dictionary<string, bool> { ["administration"] = false });
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/users");

        var cut = RenderGroups();

        var categories = cut.FindComponents<FluentNavCategory>();
        Assert.True(categories[0].Instance.Expanded);
        Assert.Contains("contains-active", categories[0].Instance.Class);
        Assert.False(categories[1].Instance.Expanded);
    }

    [Fact]
    public void NavigatingIntoACollapsedGroup_ExpandsIt()
    {
        var cut = RenderGroups();
        Assert.False(cut.FindComponents<FluentNavCategory>()[1].Instance.Expanded);

        Services.GetRequiredService<NavigationManager>().NavigateTo("/telemetry/overview");

        cut.WaitForAssertion(() => Assert.True(cut.FindComponents<FluentNavCategory>()[1].Instance.Expanded));
    }

    [Fact]
    public void IconOnlyRail_ShowsAGroupAsOneIconButtonThatStillTogglesTheGroup()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.SetupVoid("writeGroupState", _ => true).SetVoidResult();
        var cut = RenderGroups(collapsed: true);

        Assert.DoesNotContain("base-side-nav-group-chevron", cut.Markup);
        Assert.Single(cut.FindAll("button.base-side-nav-group-toggle")[0].QuerySelectorAll(".base-side-nav-sr"));

        cut.FindAll("button.base-side-nav-group-toggle")[0].Click();

        Assert.False(cut.Find("#base-nav-group-administration").HasAttribute("hidden"));
    }

    private static IReadOnlyList<NavigationItem> BottomBarOnlyItems() =>
    [
        new() { Href = "/", MatchAll = true, Label = "Home", IsMobilePrimary = true, BottomBarOrder = 0 },
        new() { Href = "/alerts", Label = "Alerts", IsMobilePrimary = true, BottomBarOnly = true, BottomBarOrder = 1 },
        new() { Href = "/more-alerts", Label = "More alerts", IsMobilePrimary = true, BottomBarOnly = true, BottomBarOrder = 2 },
        new()
        {
            Label = "Mobile only group",
            Key = "mobile-only",
            Children = [new() { Href = "/mobile-only/child", Label = "Mobile child", BottomBarOnly = true }],
        },
        new()
        {
            Label = "Mixed group",
            Key = "mixed",
            Children =
            [
                new() { Href = "/mixed/shown", Label = "Shown child" },
                new() { Href = "/mixed/hidden", Label = "Hidden child", BottomBarOnly = true },
            ],
        },
    ];

    [Fact]
    public void BottomBarOnlyItems_AreNotListedOnWideScreens()
    {
        var cut = Render(BottomBarOnlyItems());

        Assert.Contains("href=\"/\"", cut.Markup);
        Assert.DoesNotContain("href=\"/alerts\"", cut.Markup);
        Assert.DoesNotContain("href=\"/more-alerts\"", cut.Markup);
        Assert.DoesNotContain("href=\"/mobile-only/child\"", cut.Markup);
        Assert.DoesNotContain("Mobile only group", cut.Markup);
        Assert.DoesNotContain("href=\"/mixed/hidden\"", cut.Markup);
        Assert.Contains("href=\"/mixed/shown\"", cut.Markup);
    }

    [Fact]
    public void BottomBarOnlyItems_AreNotListedInTheDrawer_WhenTheyAreTabsOfTheBottomBar()
    {
        var cut = Render<BaseSideNavigation>(parameters => parameters
            .Add(p => p.Items, BottomBarOnlyItems())
            .AddCascadingValue(new NavigationLayoutContext(false, () => Task.CompletedTask, 4)));

        Assert.DoesNotContain("href=\"/alerts\"", cut.Markup);
        Assert.DoesNotContain("href=\"/more-alerts\"", cut.Markup);
    }

    [Fact]
    public void BottomBarOnlyItems_ThatDidNotBecomeATab_AreListedInTheDrawerAsMore()
    {
        var cut = Render<BaseSideNavigation>(parameters => parameters
            .Add(p => p.Items, BottomBarOnlyItems())
            .AddCascadingValue(new NavigationLayoutContext(false, () => Task.CompletedTask, 2)));

        Assert.DoesNotContain("href=\"/alerts\"", cut.Markup);
        Assert.Contains("href=\"/more-alerts\"", cut.Markup);
        Assert.Contains("href=\"/mobile-only/child\"", cut.Markup);
        Assert.Contains("href=\"/mixed/hidden\"", cut.Markup);
    }

    [Fact]
    public void BottomBarOnlyItems_AreNotListedWithoutABottomBar_EvenWhenTheLayoutContextExists()
    {
        var cut = Render<BaseSideNavigation>(parameters => parameters
            .Add(p => p.Items, BottomBarOnlyItems())
            .AddCascadingValue(new NavigationLayoutContext(false, () => Task.CompletedTask)));

        Assert.DoesNotContain("href=\"/alerts\"", cut.Markup);
        Assert.DoesNotContain("href=\"/more-alerts\"", cut.Markup);
    }
}
