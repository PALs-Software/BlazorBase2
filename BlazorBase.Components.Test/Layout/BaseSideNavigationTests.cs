using BlazorBase.Components.Layout.Navigation;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
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
        Assert.DoesNotContain("base-side-nav-section", cut.Markup);
    }

    [Fact]
    public void ShowsRoleGatedGroupAsSection_ForAdmin()
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");

        var cut = Render(SampleItems());

        Assert.Contains("base-side-nav-section", cut.Markup);
        Assert.Contains("href=\"/admin/users\"", cut.Markup);
    }
}
