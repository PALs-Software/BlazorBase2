using BlazorBase.Components.HeaderActions;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;
using Xunit;

namespace BlazorBase.Components.Test.HeaderActions;

public sealed class HeaderActionServiceTests
{
    private static HeaderActionItem Item(string id, int order = 0) => new()
    {
        Id = id,
        Icon = new Size20.Bug(),
        Title = id,
        Order = order,
        OnInvoke = _ => Task.CompletedTask,
    };

    [Fact]
    public void ItemsAreOrderedByOrderThenId()
    {
        var service = new HeaderActionService([Item("b", 10), Item("c", 5), Item("a", 10)]);

        Assert.Equal(["c", "a", "b"], service.Items.Select(item => item.Id));
    }

    [Fact]
    public void ARegisteredItemWithTheSameIdReplacesTheEarlierOne()
    {
        var service = new HeaderActionService([Item("a", 1)]);
        var changes = 0;
        service.Changed += () => changes++;

        service.Register(Item("a", 7));

        Assert.Equal(7, Assert.Single(service.Items).Order);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void UnregisterRemovesTheItemAndReportsWhetherThereWasOne()
    {
        var service = new HeaderActionService([Item("a")]);

        Assert.True(service.Unregister("a"));
        Assert.False(service.Unregister("a"));
        Assert.Empty(service.Items);
    }

    [Fact]
    public void TheDependencyInjectionRegistrationsFeedTheService()
    {
        var services = new ServiceCollection();
        services.AddHeaderAction(Item("one", 2));
        services.AddHeaderAction(_ => Item("two", 1));
        using var provider = services.BuildServiceProvider();

        var service = provider.CreateScope().ServiceProvider.GetRequiredService<IHeaderActionService>();

        Assert.Equal(["two", "one"], service.Items.Select(item => item.Id));
    }
}

public sealed class BaseHeaderActionsTests : ComponentsBunitTestContextBase
{
    private readonly BunitAuthorizationContext Authorization;
    private readonly HeaderActionService Service = new([]);
    private readonly List<string> Invoked = [];

    public BaseHeaderActionsTests()
    {
        Services.AddLocalization();
        Authorization = AddAuthorization();
        Services.AddSingleton<IHeaderActionService>(Service);
    }

    private HeaderActionItem Item(string id, int order = 0, string? role = null, string? policy = null, Func<IServiceProvider, bool>? visible = null, Func<IServiceProvider, bool>? active = null) => new()
    {
        Id = id,
        Icon = new Size20.Bug(),
        Title = "Title " + id,
        Order = order,
        RequiredRole = role,
        RequiredPolicy = policy,
        IsVisible = visible,
        IsActive = active,
        OnInvoke = _ =>
        {
            Invoked.Add(id);
            return Task.CompletedTask;
        },
    };

    private IReadOnlyList<string> RenderedIds(IRenderedComponent<BaseHeaderActions> cut) =>
        cut.FindAll(".base-header-actions > .base-header-action[data-action-id]").Select(button => button.GetAttribute("data-action-id")!).ToList();

    [Fact]
    public void ButtonsAreRenderedInOrderWithAccessibleNames()
    {
        Service.Register(Item("second", 20));
        Service.Register(Item("first", 10));

        var cut = Render<BaseHeaderActions>();

        Assert.Equal(["first", "second"], RenderedIds(cut));
        var button = cut.Find(".base-header-action[data-action-id=first]");
        Assert.Equal("Title first", button.GetAttribute("aria-label"));
        Assert.Equal("Title first", button.GetAttribute("title"));
    }

    [Fact]
    public void WithoutItemsNothingIsRendered()
    {
        var cut = Render<BaseHeaderActions>();

        Assert.Empty(cut.FindAll(".base-header-actions"));
    }

    [Fact]
    public void LayoutItemsAreMergedAndReplaceRegisteredOnesWithTheSameId()
    {
        Service.Register(Item("a", 10));
        Service.Register(Item("b", 20));

        var cut = Render<BaseHeaderActions>(parameters => parameters.Add(p => p.Items, new[] { Item("b", 5), Item("c", 30) }));

        Assert.Equal(["b", "a", "c"], RenderedIds(cut));
    }

    [Fact]
    public void AVisibilityPredicateHidesTheItem()
    {
        var visible = false;
        Service.Register(Item("a", visible: _ => visible));
        Service.Register(Item("b"));

        var cut = Render<BaseHeaderActions>();
        Assert.Equal(["b"], RenderedIds(cut));

        visible = true;
        cut.InvokeAsync(Service.Refresh);

        cut.WaitForAssertion(() => Assert.Equal(["a", "b"], RenderedIds(cut)));
    }

    [Fact]
    public void ARequiredRoleHidesTheItemFromAnonymousAndFromOtherUsers()
    {
        Service.Register(Item("admin", role: "Admin, Owner"));
        Service.Register(Item("open"));

        var anonymous = Render<BaseHeaderActions>();
        Assert.Equal(["open"], RenderedIds(anonymous));

        Authorization.SetAuthorized("bob");
        Authorization.SetRoles("User");
        var other = Render<BaseHeaderActions>();
        Assert.Equal(["open"], RenderedIds(other));
    }

    [Fact]
    public void ARequiredRoleShowsTheItemToAUserWithAnyOfTheRoles()
    {
        Service.Register(Item("admin", role: "Admin, Owner"));
        Authorization.SetAuthorized("alice");
        Authorization.SetRoles("Owner");

        var cut = Render<BaseHeaderActions>();

        Assert.Equal(["admin"], RenderedIds(cut));
    }

    [Fact]
    public void ARequiredPolicyIsEvaluatedByTheAuthorizationService()
    {
        Service.Register(Item("policy", policy: "CanReport"));
        Authorization.SetAuthorized("alice");

        var without = Render<BaseHeaderActions>();
        Assert.Empty(RenderedIds(without));

        Authorization.SetPolicies("CanReport");
        var with = Render<BaseHeaderActions>();
        Assert.Equal(["policy"], RenderedIds(with));
    }

    [Fact]
    public void ClickingInvokesTheCallbackWithTheServices()
    {
        Service.Register(Item("a"));
        var cut = Render<BaseHeaderActions>();

        cut.Find(".base-header-action[data-action-id=a]").Click();

        Assert.Equal(["a"], Invoked);
    }

    [Fact]
    public void AnActiveItemIsMarkedAsPressed()
    {
        Service.Register(Item("on", active: _ => true));
        Service.Register(Item("off", active: _ => false));
        Service.Register(Item("plain"));

        var cut = Render<BaseHeaderActions>();

        Assert.Equal("true", cut.Find(".base-header-action[data-action-id=on]").GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find(".base-header-action[data-action-id=off]").GetAttribute("aria-pressed"));
        Assert.Null(cut.Find(".base-header-action[data-action-id=plain]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void SurplusItemsMoveIntoAnOverflowMenuThatOpensAndInvokes()
    {
        Service.Register(Item("a", 1));
        Service.Register(Item("b", 2));
        Service.Register(Item("c", 3));

        var cut = Render<BaseHeaderActions>(parameters => parameters.Add(p => p.MaxInline, 2));

        Assert.Equal(["a", "b"], RenderedIds(cut));
        Assert.Empty(cut.FindAll("[role=menu]"));
        var toggle = cut.Find(".base-header-actions-toggle");
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Equal("menu", toggle.GetAttribute("aria-haspopup"));

        toggle.Click();

        var item = cut.Find("[role=menu] [role=menuitem][data-action-id=c]");
        Assert.Contains("Title c", item.TextContent);
        item.Click();

        Assert.Equal(["c"], Invoked);
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void EscapeClosesTheOverflowMenu()
    {
        Service.Register(Item("a", 1));
        Service.Register(Item("b", 2));
        var cut = Render<BaseHeaderActions>(parameters => parameters.Add(p => p.MaxInline, 1));
        cut.Find(".base-header-actions-toggle").Click();
        Assert.NotEmpty(cut.FindAll("[role=menu]"));

        cut.Find(".base-header-actions").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void NoOverflowButtonAppearsWhileEverythingFits()
    {
        Service.Register(Item("a"));

        var cut = Render<BaseHeaderActions>(parameters => parameters.Add(p => p.MaxInline, 2));

        Assert.Empty(cut.FindAll(".base-header-actions-toggle"));
    }

    [Fact]
    public void RegisteringAtRunTimeUpdatesTheRenderedRow()
    {
        var cut = Render<BaseHeaderActions>();

        cut.InvokeAsync(() => Service.Register(Item("late")));

        cut.WaitForAssertion(() => Assert.Equal(["late"], RenderedIds(cut)));
    }
}
