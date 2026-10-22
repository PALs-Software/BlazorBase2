using BlazorBase.Components.Help;
using Xunit;

namespace BlazorBase.Components.Test.Help;

public sealed class HelpMarkdownProcessorTests
{
    private static HelpProcessingContext CreateContext(
        string topic = "pages/blog",
        IDictionary<string, string>? files = null,
        string? documentationBaseUrl = null,
        IDictionary<string, string>? routes = null) => new()
    {
        Topic = topic,
        ContentRootUrl = "help/en-US/",
        Culture = "en-US",
        DocumentationBaseUrl = documentationBaseUrl,
        LoadFileAsync = (path, _) => Task.FromResult(files is not null && files.TryGetValue(path, out var content) ? content : null),
        ResolveRoute = routes is null ? null : topicName => routes.TryGetValue(topicName, out var route) ? route : null,
    };

    private static async Task<HelpDocument> ProcessAsync(string markdown, HelpProcessingContext? context = null) =>
        await HelpMarkdownProcessor.ProcessAsync(markdown, context ?? CreateContext());

    [Fact]
    public async Task FrontMatter_IsStrippedAndItsTitleIsUsed()
    {
        var document = await ProcessAsync("---\ntitle: \"Blog posts\"\nms.date: 2026-01-01\n---\n\n# Heading\n\nBody text");

        Assert.Equal("Blog posts", document.Title);
        Assert.Equal("Body text", document.Markdown);
    }

    [Fact]
    public async Task WithoutFrontMatter_TheFirstHeadingIsTheTitleAndLeavesTheBody()
    {
        var document = await ProcessAsync("\n# The page\n\nIntro\n\n## Section\n\nMore");

        Assert.Equal("The page", document.Title);
        Assert.DoesNotContain("# The page", document.Markdown);
        Assert.StartsWith("Intro", document.Markdown);
        Assert.Contains("## Section", document.Markdown);
    }

    [Fact]
    public async Task AHeadingThatIsNotFirst_StaysInTheBody()
    {
        var document = await ProcessAsync("Intro\n\n# Later heading");

        Assert.Null(document.Title);
        Assert.Contains("# Later heading", document.Markdown);
    }

    [Fact]
    public async Task CrLfLineEndings_AreHandled()
    {
        var document = await ProcessAsync("---\r\ntitle: Win\r\n---\r\n# Win\r\n\r\nText\r\n");

        Assert.Equal("Win", document.Title);
        Assert.Equal("Text", document.Markdown);
    }

    [Fact]
    public async Task DocsOnlyBlock_IsRemoved()
    {
        var document = await ProcessAsync("Keep\n\n<!-- Docs-Only-Start -->\n## Details\n\nDeep technical text\n<!-- Docs-Only-End -->\n\nAlso keep");

        Assert.DoesNotContain("Details", document.Markdown);
        Assert.DoesNotContain("Deep technical", document.Markdown);
        Assert.Contains("Keep", document.Markdown);
        Assert.Contains("Also keep", document.Markdown);
    }

    [Fact]
    public async Task DocsOnlyInline_IsRemoved()
    {
        var document = await ProcessAsync("Before <!-- Docs-Only-Start -->hidden<!-- Docs-Only-End --> after");

        Assert.Equal("Before  after", document.Markdown);
    }

    [Fact]
    public async Task InternalBlock_IsRemovedCaseInsensitively()
    {
        var document = await ProcessAsync("Public\n<!-- internal-start -->\nSecret\n<!-- INTERNAL-END -->\nPublic again");

        Assert.DoesNotContain("Secret", document.Markdown);
        Assert.Contains("Public again", document.Markdown);
    }

    [Fact]
    public async Task InternalInline_IsRemoved()
    {
        var document = await ProcessAsync("A<!-- Internal-Start --> secret<!-- Internal-End -->B");

        Assert.Equal("AB", document.Markdown);
    }

    [Fact]
    public async Task AStartMarkerWithoutEnd_RemovesTheRestOfTheDocument()
    {
        var document = await ProcessAsync("Visible\n\n<!-- Docs-Only-Start -->\nHidden forever");

        Assert.Equal("Visible", document.Markdown);
    }

    [Fact]
    public async Task AppOnlyBlock_IsUnwrapped()
    {
        var document = await ProcessAsync("Text\n\n<!-- App-Only\nClick a **bar** to select a day.\n-->\n\nEnd");

        Assert.Contains("Click a **bar** to select a day.", document.Markdown);
        Assert.DoesNotContain("App-Only", document.Markdown);
        Assert.DoesNotContain("<!--", document.Markdown);
    }

    [Fact]
    public async Task AppOnlyInline_IsUnwrapped()
    {
        var document = await ProcessAsync("Use the chart<!-- App-Only , click a bar -->.");

        Assert.Equal("Use the chart, click a bar.", document.Markdown);
    }

    [Fact]
    public async Task AppOnlyInsideDocsOnly_IsRemovedWithIt()
    {
        var document = await ProcessAsync("<!-- Docs-Only-Start -->\n<!-- App-Only\nnever shown\n-->\n<!-- Docs-Only-End -->\nShown");

        Assert.Equal("Shown", document.Markdown);
    }

    [Fact]
    public async Task OtherComments_AreRemoved()
    {
        var document = await ProcessAsync("A <!-- todo: rewrite --> B");

        Assert.Equal("A  B", document.Markdown);
    }

    [Fact]
    public async Task Include_IsReplacedByTheFileContent()
    {
        var files = new Dictionary<string, string> { ["includes/list-basics.md"] = "---\ntitle: x\n---\n## Lists\n\nSort by clicking a header." };

        var document = await ProcessAsync("Intro\n\n[!INCLUDE [List basics](../includes/list-basics.md)]\n\nOutro", CreateContext(files: files));

        Assert.Contains("## Lists", document.Markdown);
        Assert.Contains("Sort by clicking a header.", document.Markdown);
        Assert.DoesNotContain("INCLUDE", document.Markdown);
        Assert.DoesNotContain("title: x", document.Markdown);
        Assert.Contains("Outro", document.Markdown);
    }

    [Fact]
    public async Task Include_AppliesTheMarkersOfTheIncludedFile()
    {
        var files = new Dictionary<string, string> { ["includes/a.md"] = "Shown\n<!-- Docs-Only-Start -->\nHidden\n<!-- Docs-Only-End -->" };

        var document = await ProcessAsync("[!INCLUDE [a](../includes/a.md)]", CreateContext(files: files));

        Assert.Equal("Shown", document.Markdown);
    }

    [Fact]
    public async Task Include_ResolvesNestedIncludesRelativeToTheIncludingFile()
    {
        var files = new Dictionary<string, string>
        {
            ["includes/a.md"] = "A\n\n[!INCLUDE [b](deeper/b.md)]",
            ["includes/deeper/b.md"] = "B",
        };

        var document = await ProcessAsync("[!INCLUDE [a](../includes/a.md)]", CreateContext(files: files));

        Assert.Contains("A", document.Markdown);
        Assert.Contains("B", document.Markdown);
    }

    [Fact]
    public async Task Include_IgnoresCycles()
    {
        var files = new Dictionary<string, string>
        {
            ["includes/a.md"] = "A\n\n[!INCLUDE [b](b.md)]",
            ["includes/b.md"] = "B\n\n[!INCLUDE [a](a.md)]\n\n[!INCLUDE [page](../pages/blog.md)]",
        };

        var document = await ProcessAsync("[!INCLUDE [a](../includes/a.md)]", CreateContext(files: files));

        Assert.Equal(1, document.Markdown.Split('A').Length - 1);
        Assert.Contains("B", document.Markdown);
    }

    [Fact]
    public async Task Include_StopsAfterThreeLevels()
    {
        var files = new Dictionary<string, string>
        {
            ["includes/l1.md"] = "one [!INCLUDE [x](l2.md)]",
            ["includes/l2.md"] = "two [!INCLUDE [x](l3.md)]",
            ["includes/l3.md"] = "three [!INCLUDE [x](l4.md)]",
            ["includes/l4.md"] = "four",
        };

        var document = await ProcessAsync("[!INCLUDE [x](../includes/l1.md)]", CreateContext(files: files));

        Assert.Contains("three", document.Markdown);
        Assert.DoesNotContain("four", document.Markdown);
    }

    [Fact]
    public async Task Include_OfAMissingOrEscapingFile_IsDropped()
    {
        var document = await ProcessAsync("A [!INCLUDE [x](../includes/missing.md)] B [!INCLUDE [y](../../../secret.md)] C");

        Assert.Equal("A  B  C", document.Markdown);
    }

    [Fact]
    public async Task Include_InsideDocsOnly_IsNotLoaded()
    {
        var loaded = new List<string>();
        var context = new HelpProcessingContext
        {
            Topic = "pages/blog",
            ContentRootUrl = "help/en-US/",
            LoadFileAsync = (path, _) =>
            {
                loaded.Add(path);
                return Task.FromResult<string?>("x");
            },
        };

        await HelpMarkdownProcessor.ProcessAsync("<!-- Docs-Only-Start -->\n[!INCLUDE [a](../includes/a.md)]\n<!-- Docs-Only-End -->", context);

        Assert.Empty(loaded);
    }

    [Theory]
    [InlineData("NOTE", "Note", "help-callout-note")]
    [InlineData("TIP", "Tip", "help-callout-tip")]
    [InlineData("IMPORTANT", "Important", "help-callout-important")]
    [InlineData("WARNING", "Warning", "help-callout-warning")]
    [InlineData("CAUTION", "Caution", "help-callout-caution")]
    public async Task Alerts_BecomeCallouts(string kind, string title, string cssClass)
    {
        var markdown = $"> [!{kind}]\n> Be careful with **this**.\n> Second line.";
        var context = CreateContext();

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync(markdown, context), context);

        Assert.Contains("class=\"help-callout " + cssClass + "\"", html);
        Assert.Contains($"<p class=\"help-callout-title\">{title}</p>", html);
        Assert.Contains("Be careful with <strong>this</strong>.", html);
        Assert.DoesNotContain("[!", html);
    }

    [Fact]
    public async Task Alerts_UseTheTitlesOfTheContext()
    {
        var context = CreateContext();
        var localized = new HelpProcessingContext
        {
            Topic = context.Topic,
            ContentRootUrl = context.ContentRootUrl,
            LoadFileAsync = context.LoadFileAsync,
            AlertTitles = new Dictionary<string, string> { ["TIP"] = "Tipp" },
        };

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("> [!TIP]\n> Text", localized), localized);

        Assert.Contains(">Tipp</p>", html);
    }

    [Fact]
    public async Task AQuoteThatIsNoAlert_StaysAQuote()
    {
        var context = CreateContext();

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("> Just a quote", context), context);

        Assert.Contains("<blockquote>", html);
        Assert.DoesNotContain("help-callout", html);
    }

    [Fact]
    public async Task RelativeImages_LoadFromTheHelpFolder()
    {
        var context = CreateContext(topic: "pages/blog");

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("![Chart](../images/chart.png)", context), context);

        Assert.Contains("<img src=\"help/en-US/images/chart.png\" alt=\"Chart\"", html);
    }

    [Fact]
    public async Task ImagesAboveTheContentFolderOrWithUnsafeSchemes_BecomeAltText()
    {
        var context = CreateContext(topic: "pages/blog");

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("![Up](../../../x.png) ![Bad](data:image/png;base64,AAAA) ![Root](/x.png)", context), context);

        Assert.DoesNotContain("<img", html);
        Assert.Contains("Up", html);
    }

    [Fact]
    public async Task AbsoluteHttpsImages_AreKept()
    {
        var context = CreateContext();

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("![Remote](https://example.com/a.png)", context), context);

        Assert.Contains("<img src=\"https://example.com/a.png\"", html);
    }

    [Fact]
    public async Task LinksToAnotherPageTopic_NavigateToItsRouteInThePanel()
    {
        var context = CreateContext(topic: "pages/blog", routes: new Dictionary<string, string> { ["pages/blog-post"] = "/Blog/Post", ["pages/dashboard"] = "/" });

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("[Post](blog-post.md) and [Home](dashboard.md#top)", context), context);

        Assert.Contains("<a href=\"Blog/Post\">Post</a>", html);
        Assert.Contains("<a href=\"./\">Home</a>", html);
        Assert.DoesNotContain("_blank", html);
    }

    [Fact]
    public async Task OtherRelativeLinks_OpenTheDocumentationInANewTab()
    {
        var context = CreateContext(documentationBaseUrl: "https://docs.example.com/{culture}/");

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("[Sheet](../reference/limits.xlsx#limits) [Doc](../files/spec.pdf)", context), context);

        Assert.Contains("href=\"https://docs.example.com/en-US/reference/limits.xlsx#limits\" target=\"_blank\" rel=\"noopener noreferrer\"", html);
        Assert.Contains("href=\"https://docs.example.com/en-US/files/spec.pdf\"", html);
    }

    [Fact]
    public async Task OtherRelativeLinks_WithoutADocumentationUrl_ArePlainText()
    {
        var context = CreateContext();

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("See [the sheet](../reference/x.xlsx) and [here](#section).", context), context);

        Assert.DoesNotContain("<a ", html);
        Assert.Contains("See the sheet and here.", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://docs.example.com/{culture}/")]
    public async Task LinksToATopicWithoutARoute_OpenInsideThePanel(string? documentationBaseUrl)
    {
        var context = CreateContext(topic: "general/blog", documentationBaseUrl: documentationBaseUrl, routes: new Dictionary<string, string> { ["general/dashboard"] = "/" });

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("[Budgets](../concepts/budgets-and-boosts.md#boosts) and [Sibling](other-page.md)", context), context);

        Assert.Contains("<a href=\"#help:concepts/budgets-and-boosts\" data-help-topic=\"concepts/budgets-and-boosts\">Budgets</a>", html);
        Assert.Contains("<a href=\"#help:general/other-page\" data-help-topic=\"general/other-page\">Sibling</a>", html);
        Assert.DoesNotContain("_blank", html);
    }

    [Fact]
    public async Task LinksToATopicWithARoute_KeepNavigatingToThePage()
    {
        var context = CreateContext(topic: "general/blog", routes: new Dictionary<string, string> { ["general/dashboard"] = "/" });

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("[Home](dashboard.md)", context), context);

        Assert.Contains("<a href=\"./\">Home</a>", html);
        Assert.DoesNotContain("data-help-topic", html);
    }

    [Fact]
    public async Task InPanelLinks_NeverEscapeTheirAttributeOrLeaveTheHelpFolder()
    {
        var context = CreateContext();

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("[A](a\"b.md) [B](../../../secret.md) [C](/root.md)", context), context);

        Assert.Contains("data-help-topic=\"pages/a&quot;b\"", html);
        Assert.DoesNotContain("secret", html);
        Assert.DoesNotContain("root.md", html);
    }

    [Fact]
    public async Task AbsoluteLinks_OpenInANewTabAndUnsafeOnesArePlainText()
    {
        var context = CreateContext();

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("[Web](https://example.com) [Mail](mailto:a@b.de) [Bad](javascript:alert(1))", context), context);

        Assert.Contains("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\">Web</a>", html);
        Assert.Contains("<a href=\"mailto:a@b.de\">Mail</a>", html);
        Assert.DoesNotContain("javascript", html);
    }

    [Fact]
    public async Task RawHtml_IsStillEscaped()
    {
        var context = CreateContext();

        var html = HelpMarkdownProcessor.ToHtml(await ProcessAsync("<script>alert(1)</script> <img src=x onerror=alert(1)>", context), context);

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void GetDocumentationUrl_AppendsTheTopicWithHtmlExtension()
    {
        Assert.Equal("https://docs.example.com/de-DE/pages/blog.html", HelpMarkdownProcessor.GetDocumentationUrl("pages/blog", "https://docs.example.com/{culture}", "de-DE"));
        Assert.Null(HelpMarkdownProcessor.GetDocumentationUrl("pages/blog", null, "de-DE"));
        Assert.Null(HelpMarkdownProcessor.GetDocumentationUrl("pages/blog", " ", "de-DE"));
    }
}
