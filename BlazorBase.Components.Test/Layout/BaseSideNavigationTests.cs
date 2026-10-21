using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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
        Assert.DoesNotContain("base-side-nav-group-toggle", cut.Markup);
    }

    [Fact]
    public void ShowsRoleGatedGroupAsSection_ForAdmin()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");

        var cut = Render(SampleItems());

        Assert.Contains("base-side-nav-group-toggle", cut.Markup);
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

        Assert.Contains("collapsed", cut.Find("nav.base-side-nav").ClassList);
        Assert.DoesNotContain("base-side-nav-group-label", cut.Markup);
    }

    [Fact]
    public void ClickingTheNavigation_AsksTheLayoutToCloseTheDrawer()
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

        cut.Find("nav.base-side-nav").Click();

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
    public void Groups_StartCollapsed_WithTheirChildrenHiddenAndAnExpandedStateOnTheToggle()
    {
        var cut = RenderGroups();

        var toggle = cut.Find("button.base-side-nav-group-toggle");
        var items = cut.Find("#base-nav-group-administration");

        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Equal("base-nav-group-administration", toggle.GetAttribute("aria-controls"));
        Assert.True(items.HasAttribute("hidden"));
    }

    [Fact]
    public void ClickingAGroupToggle_ExpandsItAndRemembersTheChoiceInTheBrowser()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.SetupVoid("writeGroupState", _ => true).SetVoidResult();
        var cut = RenderGroups();

        cut.Find("button.base-side-nav-group-toggle").Click();

        Assert.Equal("true", cut.Find("button.base-side-nav-group-toggle").GetAttribute("aria-expanded"));
        Assert.False(cut.Find("#base-nav-group-administration").HasAttribute("hidden"));
        var written = module.Invocations["writeGroupState"].Single();
        Assert.Equal("administration", written.Arguments[0]);
        Assert.Equal(true, written.Arguments[1]);
    }

    [Fact]
    public void ClickingAGroupToggle_DoesNotAskTheLayoutToCloseTheDrawer()
    {
        var closed = 0;
        var cut = Render<BaseSideNavigation>(parameters => parameters
            .Add(p => p.Items, GroupedItems())
            .AddCascadingValue(new NavigationLayoutContext(false, () =>
            {
                closed++;
                return Task.CompletedTask;
            })));

        cut.Find("button.base-side-nav-group-toggle").Click();

        Assert.Equal(0, closed);
    }

    [Fact]
    public void RememberedStates_AreAppliedBeforeTheFirstRender()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.Setup<Dictionary<string, bool>?>("readGroupStates", _ => true)
            .SetResult(new Dictionary<string, bool> { ["telemetry"] = true });

        var cut = RenderGroups();

        var toggles = cut.FindAll("button.base-side-nav-group-toggle");
        Assert.Equal("false", toggles[0].GetAttribute("aria-expanded"));
        Assert.Equal("true", toggles[1].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void TheGroupHoldingTheCurrentPage_IsExpandedAndMarked_EvenWhenRememberedAsCollapsed()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.Setup<Dictionary<string, bool>?>("readGroupStates", _ => true)
            .SetResult(new Dictionary<string, bool> { ["administration"] = false });
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/users");

        var cut = RenderGroups();

        var toggles = cut.FindAll("button.base-side-nav-group-toggle");
        Assert.Equal("true", toggles[0].GetAttribute("aria-expanded"));
        Assert.Contains("contains-active", toggles[0].ClassList);
        Assert.Equal("false", toggles[1].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void NavigatingIntoACollapsedGroup_ExpandsIt()
    {
        var cut = RenderGroups();
        Assert.Equal("false", cut.FindAll("button.base-side-nav-group-toggle")[1].GetAttribute("aria-expanded"));

        Services.GetRequiredService<NavigationManager>().NavigateTo("/telemetry/overview");

        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.FindAll("button.base-side-nav-group-toggle")[1].GetAttribute("aria-expanded")));
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
    }}
