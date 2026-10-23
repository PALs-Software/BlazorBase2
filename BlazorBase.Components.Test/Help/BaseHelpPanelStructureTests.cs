using BlazorBase.Components.Help;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.Help;

public sealed class BaseHelpPanelStructureTests : ComponentsBunitTestContextBase
{
    private const string StructuredPage = """
        # Blog

        Intro.

        ## Tasks

        ### Publish

        Steps.

        ## Fields
        <!-- help:fields -->
        - **Title**: The headline. More.

        ## See also

        Links.
        """;

    private readonly IHelpContentSource ContentSource = Substitute.For<IHelpContentSource>();
    private readonly IHelpTopicResolver TopicResolver = Substitute.For<IHelpTopicResolver>();
    private readonly BunitJSModuleInterop PanelModule;

    public BaseHelpPanelStructureTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(ContentSource);
        Services.AddSingleton(TopicResolver);
        Services.AddSingleton<IOptions<HelpOptions>>(Options.Create(new HelpOptions()));
        PanelModule = JSInterop.SetupModule("./_content/BlazorBase.Components/js/helpPanel.js");
        TopicResolver.ResolveTopic("blog").Returns("pages/blog");
        TopicResolver.ResolveTopic("short").Returns("pages/short");
        TopicResolver.ResolveTopic("links").Returns("pages/links");
        ContentSource.GetTopicAsync("pages/blog", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("pages/blog", "en-US", StructuredPage));
        ContentSource.GetTopicAsync("pages/short", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("pages/short", "en-US", "# Short\n\nIntro.\n\n## Only one"));
        ContentSource.GetTopicAsync("pages/links", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("pages/links", "en-US", "# Links\n\nSee [Budgets](../concepts/budgets.md).\n\n## One\n\n## Two"));
        ContentSource.GetTopicAsync("concepts/budgets", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("concepts/budgets", "en-US", "# Budgets\n\nText.\n\n## Rules\n\n## Limits\n<!-- help:fields -->\n- **Cap**: The cap. More."));
    }

    private IRenderedComponent<BaseHelpPanel> RenderPanel(string path, bool isOpen = true, Action<ComponentParameterCollectionBuilder<BaseHelpPanel>>? configure = null)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        return Render<BaseHelpPanel>(parameters =>
        {
            parameters.Add(p => p.IsOpen, isOpen).Add(p => p.IsDocked, true);
            configure?.Invoke(parameters);
        });
    }

    private static void WaitForContent(IRenderedComponent<BaseHelpPanel> cut) =>
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-help-content")));

    private static void AssertTocTitles(IRenderedComponent<BaseHelpPanel> cut, params string[] titles) =>
        Assert.Equal(titles, cut.FindAll(".base-help-toc-link").Select(link => link.TextContent.Trim()));

    [Fact]
    public void ATopicWithTwoOrMoreHeadings_ShowsANestedTableOfContents()
    {
        using var english = new EnglishCulture();
        var cut = RenderPanel("blog");
        WaitForContent(cut);

        Assert.Equal("Contents", cut.Find(".base-help-toc-label").TextContent);
        AssertTocTitles(cut, "Tasks", "Publish", "Fields", "See also");
        Assert.Single(cut.FindAll(".base-help-toc-link.child"));
        Assert.Equal(["tasks", "publish", "fields", "see-also"], cut.FindAll("[data-help-toc]").Select(link => link.GetAttribute("data-help-toc")));
    }

    [Fact]
    public void ATopicWithFewerThanTwoHeadings_HasNoTableOfContents()
    {
        var cut = RenderPanel("short");
        WaitForContent(cut);

        Assert.Empty(cut.FindAll(".base-help-toc"));
    }

    [Fact]
    public void TheRenderedContent_HasHeadingIdsAndFieldEntryIds()
    {
        var cut = RenderPanel("blog");
        WaitForContent(cut);

        var content = cut.Find(".base-help-content");

        Assert.NotNull(content.QuerySelector("h2#tasks"));
        Assert.NotNull(content.QuerySelector("h3#publish"));
        Assert.NotNull(content.QuerySelector("li#field-title.help-field"));
        Assert.DoesNotContain("help:fields", content.InnerHtml);
    }

    [Fact]
    public void TheToggle_OpensAndClosesTheListAndEscapeClosesOnlyTheList()
    {
        var closed = 0;
        var cut = RenderPanel("blog", configure: parameters => parameters.Add(p => p.OnClose, EventCallback.Factory.Create(this, () => closed++)));
        WaitForContent(cut);

        Assert.Equal("false", cut.Find(".base-help-toc-toggle").GetAttribute("aria-expanded"));
        Assert.True(cut.Find(".base-help-toc-list").HasAttribute("hidden"));

        cut.Find(".base-help-toc-toggle").Click();

        Assert.Equal("true", cut.Find(".base-help-toc-toggle").GetAttribute("aria-expanded"));
        Assert.False(cut.Find(".base-help-toc-list").HasAttribute("hidden"));

        cut.Find("aside.base-help-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.True(cut.Find(".base-help-toc-list").HasAttribute("hidden"));
        Assert.Equal(0, closed);

        cut.Find("aside.base-help-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(1, closed);
    }

    [Fact]
    public void AnEntry_ClosesTheListAndScrollsToItsHeading()
    {
        var cut = RenderPanel("blog");
        WaitForContent(cut);
        cut.Find(".base-help-toc-toggle").Click();

        cut.FindAll(".base-help-toc-link").Single(link => link.TextContent.Trim() == "Fields").Click();

        Assert.True(cut.Find(".base-help-toc-list").HasAttribute("hidden"));
        cut.WaitForAssertion(() => Assert.Contains(PanelModule.Invocations["scrollToHelpAnchor"], invocation => (string?)invocation.Arguments[1] == "fields"));
    }

    [Fact]
    public void TheTableOfContentsIsBoundToTheContentForScrollSpying()
    {
        var cut = RenderPanel("blog");
        WaitForContent(cut);

        cut.WaitForAssertion(() => Assert.NotEmpty(PanelModule.Invocations["bindHelpToc"]));
    }

    [Fact]
    public void ARequestedAnchor_IsScrolledToOnceTheTopicIsShown()
    {
        var cut = RenderPanel("blog", configure: parameters => parameters
            .Add(p => p.AnchorTopic, "pages/blog")
            .Add(p => p.Anchor, "field-title")
            .Add(p => p.AnchorRequest, 1));
        WaitForContent(cut);

        cut.WaitForAssertion(() => Assert.Contains(PanelModule.Invocations["scrollToHelpAnchor"], invocation => (string?)invocation.Arguments[1] == "field-title"));
    }

    [Fact]
    public void ANewAnchorRequestOnAnOpenPanel_ScrollsAgainEvenForTheSameAnchor()
    {
        var cut = RenderPanel("blog");
        WaitForContent(cut);

        cut.Render(parameters => parameters.Add(p => p.IsOpen, true).Add(p => p.IsDocked, true).Add(p => p.Anchor, "field-title").Add(p => p.AnchorRequest, 1));
        cut.WaitForAssertion(() => Assert.Single(PanelModule.Invocations["scrollToHelpAnchor"]));

        cut.Render(parameters => parameters.Add(p => p.IsOpen, true).Add(p => p.IsDocked, true).Add(p => p.Anchor, "field-title").Add(p => p.AnchorRequest, 2));
        cut.WaitForAssertion(() => Assert.Equal(2, PanelModule.Invocations["scrollToHelpAnchor"].Count));
    }

    [Fact]
    public void AnAnchorOfAnotherTopic_OpensThatTopicWithBackAndScrollsThere()
    {
        var cut = RenderPanel("links");
        WaitForContent(cut);

        cut.Render(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.IsDocked, true)
            .Add(p => p.AnchorTopic, "concepts/budgets")
            .Add(p => p.Anchor, "field-cap")
            .Add(p => p.AnchorRequest, 1));

        cut.WaitForAssertion(() => Assert.Equal("Budgets", cut.Find(".base-help-title").TextContent));
        cut.WaitForAssertion(() => Assert.Contains(PanelModule.Invocations["scrollToHelpAnchor"], invocation => (string?)invocation.Arguments[1] == "field-cap"));
        Assert.Single(cut.FindAll(".base-help-back"));
        Assert.NotNull(cut.Find(".base-help-content").QuerySelector("li#field-cap"));
    }

    [Fact]
    public async Task OpeningATopicAtAnAnchor_ShowsTheTopicAndScrollsToTheAnchor()
    {
        var cut = RenderPanel("links");
        WaitForContent(cut);

        await cut.InvokeAsync(() => cut.Instance.OpenTopicAtAnchorAsync("concepts/budgets", "limits"));

        cut.WaitForAssertion(() => Assert.Equal("Budgets", cut.Find(".base-help-title").TextContent));
        cut.WaitForAssertion(() => Assert.Contains(PanelModule.Invocations["scrollToHelpAnchor"], invocation => (string?)invocation.Arguments[1] == "limits"));
    }

    [Fact]
    public async Task OpeningTheShownTopicAtAnAnchor_OnlyScrolls()
    {
        var cut = RenderPanel("blog");
        WaitForContent(cut);

        await cut.InvokeAsync(() => cut.Instance.OpenTopicAtAnchorAsync("pages/blog", "field-title"));

        cut.WaitForAssertion(() => Assert.Contains(PanelModule.Invocations["scrollToHelpAnchor"], invocation => (string?)invocation.Arguments[1] == "field-title"));
        Assert.Empty(cut.FindAll(".base-help-back"));
    }

    [Fact]
    public async Task TheTableOfContents_FollowsTheTopicWhenTheHistoryIsWalkedBack()
    {
        var cut = RenderPanel("links");
        WaitForContent(cut);
        AssertTocTitles(cut, "One", "Two");

        await cut.InvokeAsync(() => cut.Instance.OpenTopicAsync("concepts/budgets"));
        cut.WaitForAssertion(() => AssertTocTitles(cut, "Rules", "Limits"));

        await cut.InvokeAsync(() => cut.Find("fluent-button.base-help-back").Click());
        cut.WaitForAssertion(() => AssertTocTitles(cut, "One", "Two"));
    }
}
