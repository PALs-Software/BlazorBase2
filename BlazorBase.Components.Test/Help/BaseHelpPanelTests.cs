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

public sealed class BaseHelpPanelTests : ComponentsBunitTestContextBase
{
    private readonly IHelpContentSource ContentSource = Substitute.For<IHelpContentSource>();
    private readonly IHelpTopicResolver TopicResolver = Substitute.For<IHelpTopicResolver>();
    private readonly HelpOptions Settings = new() { DocumentationBaseUrl = "https://docs.example.com/" };

    public BaseHelpPanelTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(ContentSource);
        Services.AddSingleton(TopicResolver);
        Services.AddSingleton<IOptions<HelpOptions>>(Options.Create(Settings));
        TopicResolver.ResolveTopic(Arg.Any<string?>()).Returns(call => call.Arg<string?>() switch { "blog" => "pages/blog", "links" => "pages/links", _ => null });
        TopicResolver.GetRoute(Arg.Any<string>()).Returns(call => call.Arg<string>() == "pages/dashboard" ? "/" : null);
        ContentSource.GetTopicAsync("pages/links", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("pages/links", "en-US", "# Links\n\nSee [Budgets](../concepts/budgets.md) and [Dashboard](dashboard.md)."));
        ContentSource.GetTopicAsync("concepts/budgets", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("concepts/budgets", "en-US", "# Budgets concept\n\nBudget text. See [Roles](roles.md)."));
        ContentSource.GetTopicAsync("concepts/roles", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("concepts/roles", "en-US", "# Roles concept\n\nRoles text."));
        ContentSource.GetTopicAsync("concepts/missing", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("getting-started", "en-US", "# Start\n\nWelcome."));
        ContentSource.GetTopicAsync("pages/blog", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("pages/blog", "en-US", "---\ntitle: Blog help\n---\n# Blog\n\nWrite **posts** here.\n\n> [!NOTE]\n> Mind the draft state."));
        ContentSource.GetTopicAsync("getting-started", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("getting-started", "en-US", "# Start\n\nWelcome."));
    }

    private IRenderedComponent<BaseHelpPanel> RenderPanel(bool isOpen = true, bool isDocked = true, Action? onClose = null) =>
        Render<BaseHelpPanel>(parameters => parameters
            .Add(p => p.IsOpen, isOpen)
            .Add(p => p.IsDocked, isDocked)
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => onClose?.Invoke())));

    private void NavigateTo(string path) => Services.GetRequiredService<NavigationManager>().NavigateTo(path);

    private async Task OpenTopicAsync(IRenderedComponent<BaseHelpPanel> cut, string topic) =>
        await cut.InvokeAsync(() => cut.Instance.OpenTopicAsync(topic));

    private static void AssertTitle(IRenderedComponent<BaseHelpPanel> cut, string title) =>
        cut.WaitForAssertion(() => Assert.Equal(title, cut.Find(".base-help-title").TextContent));

    [Fact]
    public void ALinkToATopicWithoutARoute_IsAnInPanelLinkAndALinkToAPageStaysANavigation()
    {
        NavigateTo("links");

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".base-help-content a").Count));
        var inPanel = cut.Find(".base-help-content a[data-help-topic]");
        Assert.Equal("concepts/budgets", inPanel.GetAttribute("data-help-topic"));
        Assert.Equal("#help:concepts/budgets", inPanel.GetAttribute("href"));
        Assert.Null(inPanel.GetAttribute("target"));
        Assert.Equal("./", cut.Find(".base-help-content a:not([data-help-topic])").GetAttribute("href"));
    }

    [Fact]
    public void WithoutAPreviousTopic_NoBackButtonIsShown()
    {
        NavigateTo("links");

        var cut = RenderPanel();

        AssertTitle(cut, "Links");
        Assert.Empty(cut.FindAll(".base-help-back"));
    }

    [Fact]
    public async Task OpeningATopicInThePanel_ShowsItWithABackButtonThatReturnsToThePreviousOne()
    {
        using var english = new EnglishCulture();
        NavigateTo("links");
        var cut = RenderPanel();
        AssertTitle(cut, "Links");

        await OpenTopicAsync(cut, "concepts/budgets");

        AssertTitle(cut, "Budgets concept");
        Assert.Contains("Budget text.", cut.Find(".base-help-content").InnerHtml);
        Assert.Equal("https://docs.example.com/concepts/budgets.html", cut.Find("a.base-help-docs-link").GetAttribute("href"));
        Assert.Equal("Back to the previous help topic", cut.Find("fluent-button.base-help-back").GetAttribute("title"));

        await cut.InvokeAsync(() => cut.Find("fluent-button.base-help-back").Click());

        AssertTitle(cut, "Links");
        Assert.Empty(cut.FindAll(".base-help-back"));
        Assert.Equal("https://docs.example.com/pages/links.html", cut.Find("a.base-help-docs-link").GetAttribute("href"));
    }

    [Fact]
    public async Task TopicsOpenedOneAfterTheOther_AreWalkedBackInReverseOrder()
    {
        NavigateTo("links");
        var cut = RenderPanel();
        AssertTitle(cut, "Links");

        await OpenTopicAsync(cut, "concepts/budgets");
        AssertTitle(cut, "Budgets concept");
        await OpenTopicAsync(cut, "concepts/roles");
        AssertTitle(cut, "Roles concept");

        await cut.InvokeAsync(() => cut.Find("fluent-button.base-help-back").Click());
        AssertTitle(cut, "Budgets concept");
        Assert.Single(cut.FindAll(".base-help-back"));

        await cut.InvokeAsync(() => cut.Find("fluent-button.base-help-back").Click());
        AssertTitle(cut, "Links");
        Assert.Empty(cut.FindAll(".base-help-back"));
    }

    [Fact]
    public async Task OpeningTheTopicThatIsAlreadyShown_DoesNotGrowTheHistory()
    {
        NavigateTo("links");
        var cut = RenderPanel();
        AssertTitle(cut, "Links");
        await OpenTopicAsync(cut, "concepts/budgets");
        AssertTitle(cut, "Budgets concept");

        await OpenTopicAsync(cut, "concepts/budgets");
        await cut.InvokeAsync(() => cut.Find("fluent-button.base-help-back").Click());

        AssertTitle(cut, "Links");
        Assert.Empty(cut.FindAll(".base-help-back"));
    }

    [Fact]
    public async Task ATopicThatIsNotAPlainRelativePath_IsIgnored()
    {
        NavigateTo("links");
        var cut = RenderPanel();
        AssertTitle(cut, "Links");

        await OpenTopicAsync(cut, "../secret");
        await OpenTopicAsync(cut, "/etc/passwd");
        await OpenTopicAsync(cut, "");

        AssertTitle(cut, "Links");
        Assert.Empty(cut.FindAll(".base-help-back"));
    }

    [Fact]
    public async Task ATopicWithoutContent_ShowsTheEmptyStateInsteadOfTheFallbackTopic_AndBackStillWorks()
    {
        NavigateTo("links");
        var cut = RenderPanel();
        AssertTitle(cut, "Links");

        await OpenTopicAsync(cut, "concepts/missing");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-help-empty")));
        Assert.Single(cut.FindAll(".base-help-back"));

        await cut.InvokeAsync(() => cut.Find("fluent-button.base-help-back").Click());

        AssertTitle(cut, "Links");
    }

    [Fact]
    public async Task ClosingThePanel_ClearsTheHistory()
    {
        NavigateTo("links");
        var cut = RenderPanel();
        AssertTitle(cut, "Links");
        await OpenTopicAsync(cut, "concepts/budgets");
        AssertTitle(cut, "Budgets concept");

        cut.Render(parameters => parameters.Add(p => p.IsOpen, false));
        cut.Render(parameters => parameters.Add(p => p.IsOpen, true));

        AssertTitle(cut, "Links");
        Assert.Empty(cut.FindAll(".base-help-back"));
    }

    [Fact]
    public async Task NavigatingToAnotherPage_ClearsTheHistoryAndShowsTheTopicOfThatPage()
    {
        NavigateTo("links");
        var cut = RenderPanel();
        AssertTitle(cut, "Links");
        await OpenTopicAsync(cut, "concepts/budgets");
        AssertTitle(cut, "Budgets concept");

        NavigateTo("blog");

        AssertTitle(cut, "Blog help");
        Assert.Empty(cut.FindAll(".base-help-back"));
    }

    [Fact]
    public async Task NavigatingToAPageWithTheSameTopic_ReturnsToThePageTopic()
    {
        NavigateTo("somewhere");
        var cut = RenderPanel();
        AssertTitle(cut, "Start");
        await OpenTopicAsync(cut, "concepts/budgets");
        AssertTitle(cut, "Budgets concept");

        NavigateTo("elsewhere");

        AssertTitle(cut, "Start");
        Assert.Empty(cut.FindAll(".base-help-back"));
    }

    [Fact]
    public async Task ChangingOnlyTheQueryOfThePage_KeepsTheOpenedTopic()
    {
        NavigateTo("links");
        var cut = RenderPanel();
        AssertTitle(cut, "Links");
        await OpenTopicAsync(cut, "concepts/budgets");
        AssertTitle(cut, "Budgets concept");

        NavigateTo("links?page=2");

        AssertTitle(cut, "Budgets concept");
        Assert.Single(cut.FindAll(".base-help-back"));
    }

    [Fact]
    public void OpeningThePanel_BindsTheLinkHandlerOnce()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.SetupVoid("bindHelpLinks", _ => true).SetVoidResult();

        RenderPanel(isDocked: true);

        Assert.Single(module.Invocations["bindHelpLinks"]);
    }

    [Fact]
    public async Task TheBackButtonIsGermanUnderTheGermanCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("de");

        try
        {
            NavigateTo("links");
            var cut = RenderPanel();
            cut.WaitForAssertion(() => Assert.Contains("Budgets", cut.Markup));
            await OpenTopicAsync(cut, "concepts/budgets");

            cut.WaitForAssertion(() => Assert.Equal("Zurück zum vorherigen Hilfethema", cut.Find("fluent-button.base-help-back").GetAttribute("title")));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Closed_RendersNothing()
    {
        var cut = RenderPanel(isOpen: false);

        Assert.Empty(cut.Markup.Trim());
        ContentSource.DidNotReceiveWithAnyArgs().GetTopicAsync(default!, default!, default);
    }

    [Fact]
    public void Open_ShowsTheTitleAndTheRenderedTopicOfTheCurrentPage()
    {
        NavigateTo("blog");

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Assert.Equal("Blog help", cut.Find(".base-help-title").TextContent));
        Assert.Contains("<strong>posts</strong>", cut.Find(".base-help-content").InnerHtml);
        Assert.Contains("help-callout-note", cut.Find(".base-help-content blockquote").ClassList);
        Assert.DoesNotContain("Blog</h1>", cut.Markup);
    }

    [Fact]
    public void ALinkToTheFullDocumentation_PointsAtTheTopicOfTheShownContent()
    {
        NavigateTo("blog");

        var cut = RenderPanel();

        cut.WaitForAssertion(() =>
        {
            var link = cut.Find("a.base-help-docs-link");
            Assert.Equal("https://docs.example.com/pages/blog.html", link.GetAttribute("href"));
            Assert.Equal("_blank", link.GetAttribute("target"));
            Assert.Contains("noopener", link.GetAttribute("rel"));
        });
    }

    [Fact]
    public void WithoutADocumentationUrl_NoLinkIsOffered()
    {
        Settings.DocumentationBaseUrl = null;
        NavigateTo("blog");

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Assert.Contains("Write", cut.Markup));
        Assert.Empty(cut.FindAll("a.base-help-docs-link"));
    }

    [Fact]
    public void APageWithoutATopic_ShowsTheFallbackTopic()
    {
        NavigateTo("somewhere-else");

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Assert.Contains("Welcome.", cut.Find(".base-help-content").InnerHtml));
        Assert.Equal("Start", cut.Find(".base-help-title").TextContent);
    }

    [Fact]
    public void WithoutAnyContent_ShowsTheEmptyState()
    {
        using var english = new EnglishCulture();
        ContentSource.GetTopicAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((HelpSourceDocument?)null);

        var cut = RenderPanel();

        cut.WaitForAssertion(() => Assert.Equal("No help is available for this page yet.", cut.Find(".base-help-empty").TextContent));
        Assert.Equal("Help", cut.Find(".base-help-title").TextContent);
    }

    [Fact]
    public void UnderTheGermanCulture_TheTextsAreGerman()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("de");

        try
        {
            ContentSource.GetTopicAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((HelpSourceDocument?)null);

            var cut = RenderPanel();

            cut.WaitForAssertion(() => Assert.Equal("Für diese Seite ist noch keine Hilfe verfügbar.", cut.Find(".base-help-empty").TextContent));
            Assert.Equal("Hilfe", cut.Find(".base-help-title").TextContent);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task WhileTheContentLoads_ShowsALoadingState()
    {
        var pending = new TaskCompletionSource<HelpSourceDocument?>();
        ContentSource.GetTopicAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(pending.Task);

        var cut = RenderPanel();

        Assert.NotEmpty(cut.FindAll(".base-help-status"));
        Assert.Equal("true", cut.Find(".base-help-body").GetAttribute("aria-busy"));

        await cut.InvokeAsync(() => pending.SetResult(new HelpSourceDocument("getting-started", "en-US", "Done")));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".base-help-status")));
    }

    [Fact]
    public void NavigatingWhileOpen_UpdatesTheContent()
    {
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.Equal("Start", cut.Find(".base-help-title").TextContent));

        NavigateTo("blog");

        cut.WaitForAssertion(() => Assert.Equal("Blog help", cut.Find(".base-help-title").TextContent));
    }

    [Fact]
    public void NavigatingWithinTheSameTopic_DoesNotReload()
    {
        NavigateTo("blog");
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.Equal("Blog help", cut.Find(".base-help-title").TextContent));

        NavigateTo("blog?page=2");

        ContentSource.Received(1).GetTopicAsync("pages/blog", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseButtonEscapeAndTheDimmedPage_AskTheHostToClose()
    {
        var closed = 0;
        var overlay = RenderPanel(isDocked: false, onClose: () => closed++);

        await overlay.InvokeAsync(() => overlay.Find("fluent-button.base-help-close").Click());
        await overlay.InvokeAsync(() => overlay.Find("aside").KeyDown(new KeyboardEventArgs { Key = "Escape" }));
        await overlay.InvokeAsync(() => overlay.Find(".base-help-overlay").Click());

        Assert.Equal(3, closed);
    }

    [Fact]
    public void Docked_IsANonModalComplementaryRegionWithoutADimmedPage()
    {
        var cut = RenderPanel(isDocked: true);

        var panel = cut.Find("aside.base-help-panel");
        Assert.Equal("complementary", panel.GetAttribute("role"));
        Assert.Null(panel.GetAttribute("aria-modal"));
        Assert.Contains("docked", panel.ClassList);
        Assert.Empty(cut.FindAll(".base-help-overlay"));
    }

    [Fact]
    public void AsASheet_IsAModalDialogLabelledByItsTitleOverADimmedPage()
    {
        var cut = RenderPanel(isDocked: false);

        var panel = cut.Find("aside.base-help-panel");
        Assert.Equal("dialog", panel.GetAttribute("role"));
        Assert.Equal("true", panel.GetAttribute("aria-modal"));
        Assert.Equal("base-help-title", panel.GetAttribute("aria-labelledby"));
        Assert.Contains("overlay", panel.ClassList);
        Assert.Single(cut.FindAll(".base-help-overlay"));
    }

    [Fact]
    public void OpeningAsASheet_MovesTheFocusIntoThePanelAndKeepsItThere()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.SetupVoid("trapFocus", _ => true).SetVoidResult();

        RenderPanel(isDocked: false);

        Assert.Single(module.Invocations["trapFocus"]);
    }

    [Fact]
    public void OpeningDocked_FocusesThePanelWithoutTrappingIt()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.SetupVoid("trapFocus", _ => true).SetVoidResult();

        RenderPanel(isDocked: true);

        Assert.Empty(module.Invocations["trapFocus"]);
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
    }
}
