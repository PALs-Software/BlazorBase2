using BlazorBase.User.Layout.Navigation;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Test.Layout;

public sealed class BaseBottomNavigationTests : UserBunitTestContextBase
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
}
