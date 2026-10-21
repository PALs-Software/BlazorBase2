using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Components.Test.Layout;

public sealed class BaseBottomNavigationTests : ComponentsBunitTestContextBase
{
    private static IReadOnlyList<NavigationItem> SampleItems(Func<Task>? onLogout = null) =>
    [
        new() { Href = "/", MatchAll = true, Label = "Home", IsMobilePrimary = true },
        new() { Href = "/projects", Label = "Projects", IsMobilePrimary = true },
        new() { Href = "/work-items", Label = "Work items" },
        new()
        {
            Label = "Admin",
            RequiredRole = "Admin",
            Children =
            [
                new() { Href = "/admin/users", Label = "Users" },
                new() { Href = "/admin/tokens", Label = "Tokens" },
            ],
        },
        new() { Label = "Logout", IsDanger = true, OnClick = onLogout ?? (() => Task.CompletedTask) },
    ];

    private IRenderedComponent<BaseBottomNavigation> Render(IReadOnlyList<NavigationItem> items)
    {
        Services.AddLocalization();
        return Render<BaseBottomNavigation>(parameters => parameters.Add(p => p.Items, items));
    }

    [Fact]
    public void PrimaryItems_RenderAsTabs_SecondaryStayInClosedSheet()
    {
        var cut = Render(SampleItems());
        var markup = cut.Markup;

        Assert.Contains("href=\"/\"", markup);
        Assert.Contains("href=\"/projects\"", markup);
        Assert.Contains("base-bottom-more", markup);
        Assert.DoesNotContain("href=\"/work-items\"", markup);
    }

    [Fact]
    public async Task MoreSheet_ShowsSecondaryLinksAndActions()
    {
        var cut = Render(SampleItems());

        await cut.InvokeAsync(() => cut.Find("button.base-bottom-more").Click());

        Assert.Contains("base-more-sheet", cut.Markup);
        Assert.Contains("href=\"/work-items\"", cut.Markup);
        Assert.Contains("base-more-danger", cut.Markup);
    }

    [Fact]
    public async Task AdminGroup_HiddenForNonAdmin()
    {
        AddAuthorization().SetAuthorized("user");

        var cut = Render(SampleItems());
        await cut.InvokeAsync(() => cut.Find("button.base-bottom-more").Click());

        Assert.DoesNotContain("base-more-group", cut.Markup);
    }

    [Fact]
    public async Task AdminGroup_DrillsDownToChildrenAndBack()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");

        var cut = Render(SampleItems());
        await cut.InvokeAsync(() => cut.Find("button.base-bottom-more").Click());

        Assert.DoesNotContain("href=\"/admin/users\"", cut.Markup);

        await cut.InvokeAsync(() => cut.Find("button.base-more-group").Click());
        Assert.Contains("base-more-back", cut.Markup);
        Assert.Contains("href=\"/admin/users\"", cut.Markup);
        Assert.DoesNotContain("href=\"/work-items\"", cut.Markup);

        await cut.InvokeAsync(() => cut.Find("button.base-more-back").Click());
        Assert.Contains("href=\"/work-items\"", cut.Markup);
        Assert.DoesNotContain("href=\"/admin/users\"", cut.Markup);
    }

    [Fact]
    public async Task LogoutAction_InvokesCallback_AndClosesSheet()
    {
        var loggedOut = false;
        var cut = Render(SampleItems(() =>
        {
            loggedOut = true;
            return Task.CompletedTask;
        }));

        await cut.InvokeAsync(() => cut.Find("button.base-bottom-more").Click());
        await cut.InvokeAsync(() => cut.Find("button.base-more-danger").Click());

        Assert.True(loggedOut);
        Assert.DoesNotContain("base-more-sheet", cut.Markup);
    }

    /// <summary>
    /// The tab bar is the only navigation a phone shows, and a screen reader reads it as a row of equal
    /// links unless one of them carries <c>aria-current</c>. The active CSS class alone says nothing.
    /// </summary>
    [Fact]
    public void ActiveTab_IsTheOnlyOneMarkedAsTheCurrentPage()
    {
        var cut = Render(SampleItems());

        var home = cut.Find("a[href=\"/\"]");
        var projects = cut.Find("a[href=\"/projects\"]");

        Assert.Equal("page", home.GetAttribute("aria-current"));
        Assert.Null(projects.GetAttribute("aria-current"));
    }

    /// <summary>
    /// A route below a tab keeps that tab current, and the home tab — which matches the whole route —
    /// must let go of it. Deriving the attribute from <c>MatchAll</c> alone marked home as the current
    /// page on every route in the application.
    /// </summary>
    [Fact]
    public void NestedRoute_MovesTheCurrentPageOntoThePrefixTab()
    {
        Services.AddLocalization();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/projects/42");

        var cut = Render<BaseBottomNavigation>(parameters => parameters.Add(p => p.Items, SampleItems()));

        Assert.Null(cut.Find("a[href=\"/\"]").GetAttribute("aria-current"));
        Assert.Equal("page", cut.Find("a[href=\"/projects\"]").GetAttribute("aria-current"));
    }

    [Fact]
    public async Task SheetLink_IsMarkedAsTheCurrentPage_OnItsOwnRoute()
    {
        Services.AddLocalization();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/work-items");

        var cut = Render<BaseBottomNavigation>(parameters => parameters.Add(p => p.Items, SampleItems()));
        await cut.InvokeAsync(() => cut.Find("button.base-bottom-more").Click());

        Assert.Equal("page", cut.Find("a[href=\"/work-items\"]").GetAttribute("aria-current"));
    }

    /// <summary>
    /// The bar has to notice a navigation it did not cause itself — a link in the page body, the back
    /// button — or the marker stays on whatever tab was current when it first rendered.
    /// </summary>
    [Fact]
    public async Task NavigationElsewhere_MovesTheMarker()
    {
        var cut = Render(SampleItems());
        Assert.Equal("page", cut.Find("a[href=\"/\"]").GetAttribute("aria-current"));

        await cut.InvokeAsync(() => Services.GetRequiredService<NavigationManager>().NavigateTo("/projects"));

        Assert.Null(cut.Find("a[href=\"/\"]").GetAttribute("aria-current"));
        Assert.Equal("page", cut.Find("a[href=\"/projects\"]").GetAttribute("aria-current"));
    }

    /// <summary>
    /// An empty href is how Blazor spells the application root, and it is what the project template
    /// writes. Treating it as "no route" left the home tab unmarked on the one page it is current for.
    /// </summary>
    [Fact]
    public void EmptyHref_IsTheApplicationRoot_NotAMissingRoute()
    {
        Services.AddLocalization();

        var cut = Render<BaseBottomNavigation>(parameters => parameters.Add(p => p.Items,
        [
            new NavigationItem { Href = string.Empty, MatchAll = true, Label = "Home", IsMobilePrimary = true },
            new NavigationItem { Href = "notes", Label = "Notes", IsMobilePrimary = true },
        ]));

        Assert.Equal("page", cut.Find("a[href=\"\"]").GetAttribute("aria-current"));
        Assert.Null(cut.Find("a[href=\"notes\"]").GetAttribute("aria-current"));
    }

    /// <summary>
    /// A group or an action does not navigate at all, so nothing about it is ever the current page.
    /// </summary>
    [Fact]
    public async Task GroupsAndActions_AreNeverMarkedAsTheCurrentPage()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");

        var cut = Render(SampleItems());
        await cut.InvokeAsync(() => cut.Find("button.base-bottom-more").Click());

        Assert.Empty(cut.FindAll("button.base-more-item[aria-current]"));
    }

    private IRenderedComponent<BaseBottomNavigation> RenderWith(
        IReadOnlyList<NavigationItem> items,
        Action<ComponentParameterCollectionBuilder<BaseBottomNavigation>>? configure = null)
    {
        Services.AddLocalization();
        return Render<BaseBottomNavigation>(parameters =>
        {
            parameters.Add(p => p.Items, items);
            configure?.Invoke(parameters);
        });
    }

    private static IReadOnlyList<string> TabHrefs(IRenderedComponent<BaseBottomNavigation> cut) =>
        cut.FindAll("a.base-bottom-tab").Select(tab => tab.GetAttribute("href")!).ToList();

    [Fact]
    public void Tabs_AreOrderedByBottomBarOrder_ThenByListOrder()
    {
        var cut = RenderWith(
        [
            new() { Href = "/a", Label = "A", IsMobilePrimary = true, BottomBarOrder = 2 },
            new() { Href = "/b", Label = "B", IsMobilePrimary = true, BottomBarOrder = 1 },
            new() { Href = "/c", Label = "C", IsMobilePrimary = true, BottomBarOrder = 1 },
        ]);

        Assert.Equal(["/b", "/c", "/a"], TabHrefs(cut));
    }

    [Fact]
    public void FlaggedItemsInsideAGroup_BecomeTabsToo_AndRespectTheRoleFilter()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("user");
        var items = new List<NavigationItem>
        {
            new() { Href = "/", Label = "Home", MatchAll = true, IsMobilePrimary = true },
            new()
            {
                Label = "Copilot",
                Children =
                [
                    new() { Href = "/budgets", Label = "Budgets", IsMobilePrimary = true, RequiredRole = "Admin" },
                    new() { Href = "/usage", Label = "Usage", IsMobilePrimary = true },
                ],
            },
        };

        var cut = RenderWith(items);

        Assert.Equal(["/", "/usage"], TabHrefs(cut));
    }

    [Fact]
    public void Tabs_AreCappedAtMaxPrimaryItems()
    {
        var items = Enumerable.Range(1, 6)
            .Select(i => new NavigationItem { Href = $"/p{i}", Label = $"P{i}", IsMobilePrimary = true })
            .ToList();

        var cut = RenderWith(items, parameters => parameters.Add(p => p.MaxPrimaryItems, 3));

        Assert.Equal(3, TabHrefs(cut).Count);
    }

    [Fact]
    public void WithoutAnyFlaggedItem_TheFirstTopLevelLinksFillTheBar()
    {
        var items = new List<NavigationItem>
        {
            new() { Label = "Logout", OnClick = () => Task.CompletedTask },
            new() { Href = "/a", Label = "A" },
            new() { Label = "Group", Children = [new() { Href = "/nested", Label = "Nested" }] },
            new() { Href = "/b", Label = "B" },
            new() { Href = "/c", Label = "C" },
            new() { Href = "/d", Label = "D" },
            new() { Href = "/e", Label = "E" },
        };

        var cut = RenderWith(items);

        Assert.Equal(["/a", "/b", "/c", "/d"], TabHrefs(cut));
    }

    [Fact]
    public void ShortLabel_IsShownOnTheTab_WhileTheFullLabelStaysAsTitle()
    {
        var cut = RenderWith([new() { Href = "/stats", Label = "My AI Statistics", ShortLabel = "AI Stats", IsMobilePrimary = true }]);

        var tab = cut.Find("a.base-bottom-tab");
        Assert.Equal("AI Stats", tab.QuerySelector(".base-bottom-tab-label")!.TextContent);
        Assert.Equal("My AI Statistics", tab.GetAttribute("title"));
    }

    [Fact]
    public async Task OnMore_ReplacesTheSheet_AndMirrorsTheExpandedStateOfWhatItReveals()
    {
        var raised = 0;
        var cut = RenderWith(SampleItems(), parameters => parameters
            .Add(p => p.OnMore, EventCallback.Factory.Create(this, () => raised++))
            .Add(p => p.IsMoreExpanded, true)
            .Add(p => p.MoreControls, "base-navigation"));

        var more = cut.Find("button.base-bottom-more");
        Assert.Equal("true", more.GetAttribute("aria-expanded"));
        Assert.Equal("base-navigation", more.GetAttribute("aria-controls"));
        Assert.Null(more.GetAttribute("aria-haspopup"));

        await cut.InvokeAsync(() => more.Click());

        Assert.Equal(1, raised);
        Assert.DoesNotContain("base-more-sheet", cut.Markup);
    }

    [Fact]
    public void OnMore_ShowsTheMoreTabEvenWithoutSecondaryItems()
    {
        var cut = RenderWith(
            [new() { Href = "/", Label = "Home", IsMobilePrimary = true }],
            parameters => parameters.Add(p => p.OnMore, EventCallback.Factory.Create(this, () => { })));

        Assert.Single(cut.FindAll("button.base-bottom-more"));
    }
}
