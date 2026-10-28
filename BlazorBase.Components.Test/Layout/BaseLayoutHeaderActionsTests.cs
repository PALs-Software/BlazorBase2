using BlazorBase.Components.HeaderActions;
using BlazorBase.Components.Layout;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.Layout;

public sealed class BaseLayoutHeaderActionsTests : ComponentsBunitTestContextBase
{
    public BaseLayoutHeaderActionsTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(Substitute.For<IThemeService>());
        Services.AddSingleton(Substitute.For<ILanguageService>());
        AddAuthorization();
    }

    private static HeaderActionItem Item(string id, int order, Action? onInvoke = null) => new()
    {
        Id = id,
        Icon = new Size20.Bug(),
        Title = "Title " + id,
        Order = order,
        OnInvoke = _ =>
        {
            onInvoke?.Invoke();
            return Task.CompletedTask;
        },
    };

    [Fact]
    public void WithoutRegistrationsAndItemsNoHeaderActionsAreRendered()
    {
        var cut = Render<BaseLayout>(parameters => parameters.Add(p => p.AppTitle, "Shell"));

        Assert.Empty(cut.FindAll(".base-header-actions"));
    }

    [Fact]
    public void ParameterItemsAreRenderedInTheHeaderInOrder()
    {
        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.HeaderActions, new[] { Item("b", 20), Item("a", 10) }));

        var ids = cut.FindAll("header .base-header-actions fluent-button[data-action-id]").Select(button => button.GetAttribute("data-action-id"));

        Assert.Equal(["a", "b"], ids);
    }

    [Fact]
    public void RegisteredItemsAreRenderedBeforeTheHelpAndSettingsButtons()
    {
        Services.AddHeaderAction(Item("reg", 1));

        var cut = Render<BaseLayout>(parameters => parameters.Add(p => p.AppTitle, "Shell"));

        var end = cut.Find(".base-header-end");
        Assert.NotNull(end.QuerySelector(".base-header-actions fluent-button[data-action-id=reg]"));
        var html = end.InnerHtml;
        Assert.True(html.IndexOf("data-action-id=\"reg\"", StringComparison.Ordinal) < html.IndexOf("base-settings-toggle", StringComparison.Ordinal));
    }

    [Fact]
    public void AHeaderActionCanBeClicked()
    {
        var clicked = false;

        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.HeaderActions, new[] { Item("a", 1, () => clicked = true) }));
        cut.Find("fluent-button[data-action-id=a]").Click();

        Assert.True(clicked);
    }

    [Fact]
    public void LayoutOverlaysAreRenderedAfterTheMainContentInsideTheLayout()
    {
        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.LayoutOverlays, (RenderFragment)(builder => builder.AddMarkupContent(0, "<aside id=\"overlay\">Panel</aside>")))
            .AddChildContent("<p id=\"page\">Page</p>"));

        var overlay = cut.Find(".layout-root .main-content > #overlay");
        Assert.Equal("Panel", overlay.TextContent);
        var html = cut.Find(".main-content").InnerHtml;
        Assert.True(html.IndexOf("id=\"base-main\"", StringComparison.Ordinal) < html.IndexOf("id=\"overlay\"", StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutOverlaysNothingExtraIsRendered()
    {
        var cut = Render<BaseLayout>(parameters => parameters.Add(p => p.AppTitle, "Shell"));

        Assert.Empty(cut.FindAll("#overlay"));
    }
}
