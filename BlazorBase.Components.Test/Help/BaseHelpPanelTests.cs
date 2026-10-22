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
        TopicResolver.ResolveTopic(Arg.Any<string?>()).Returns(call => call.Arg<string?>() == "blog" ? "pages/blog" : null);
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
