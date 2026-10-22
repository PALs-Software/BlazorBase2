using BlazorBase.Components.Html;
using Xunit;

namespace BlazorBase.Components.Test.Html;

public sealed class MarkdownConverterTrustedTests
{
    private static readonly TrustedMarkdownOptions Trusted = new() { AllowImages = true, OpenExternalLinksInNewTab = true };

    [Fact]
    public void TheDefaultMode_StillTurnsImagesIntoLinks()
    {
        var html = MarkdownConverter.ToHtml("![Alt](https://example.com/a.png)");

        Assert.DoesNotContain("<img", html);
        Assert.Contains("<a href=\"https://example.com/a.png\">Alt</a>", html);
    }

    [Fact]
    public void Trusted_RendersImages_ButOnlyWithRelativeOrWebUrls()
    {
        var html = MarkdownConverter.ToHtml("![A](images/a.png) ![B](https://example.com/b.png) ![C](data:image/png;base64,AAAA) ![D](javascript:alert(1))", Trusted);

        Assert.Contains("<img src=\"images/a.png\" alt=\"A\" />", html);
        Assert.Contains("<img src=\"https://example.com/b.png\" alt=\"B\" />", html);
        Assert.DoesNotContain("data:", html);
        Assert.DoesNotContain("javascript", html);
    }

    [Fact]
    public void Trusted_WithoutAllowImages_KeepsTheRestrictiveImageBehavior()
    {
        var html = MarkdownConverter.ToHtml("![A](images/a.png)", new TrustedMarkdownOptions());

        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void Trusted_OpensWebLinksInANewTab_ButNotMailOrRelativeLinks()
    {
        var html = MarkdownConverter.ToHtml("[W](https://example.com) [M](mailto:a@b.de) [R](page.md)", Trusted);

        Assert.Contains("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\">W</a>", html);
        Assert.Contains("<a href=\"mailto:a@b.de\">M</a>", html);
        Assert.Contains("<a href=\"page.md\">R</a>", html);
    }

    [Fact]
    public void Trusted_StillEscapesRawHtmlAndDropsUnsafeLinks()
    {
        var html = MarkdownConverter.ToHtml("<script>x</script> [Bad](javascript:alert(1))", Trusted);

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("javascript", html);
    }

    [Fact]
    public void Trusted_ResolversRewriteImagesAndRelativeLinks()
    {
        var options = new TrustedMarkdownOptions
        {
            AllowImages = true,
            ResolveImageUrl = url => url == "gone.png" ? null : "root/" + url,
            ResolveLinkUrl = url => url == "none" ? null : new MarkdownLink("/x/" + url, OpenInNewTab: true),
        };

        var html = MarkdownConverter.ToHtml("![A](a.png) ![G](gone.png) [L](one) [N](none) [Web](https://example.com)", options);

        Assert.Contains("<img src=\"root/a.png\" alt=\"A\" />", html);
        Assert.Contains("G", html);
        Assert.DoesNotContain("gone.png", html);
        Assert.Contains("<a href=\"/x/one\" target=\"_blank\" rel=\"noopener noreferrer\">L</a>", html);
        Assert.DoesNotContain("href=\"none\"", html);
        Assert.Contains("<a href=\"https://example.com\">Web</a>", html);
    }

    [Fact]
    public void Trusted_AResolverCannotSmuggleAnUnsafeUrl()
    {
        var options = new TrustedMarkdownOptions
        {
            AllowImages = true,
            ResolveImageUrl = _ => "javascript:alert(1)",
            ResolveLinkUrl = _ => new MarkdownLink("javascript:alert(1)"),
        };

        var html = MarkdownConverter.ToHtml("![A](a.png) [L](l)", options);

        Assert.DoesNotContain("javascript", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void Alerts_AreOnlyConvertedWhenTitlesAreGiven()
    {
        const string markdown = "> [!NOTE]\n> Text";

        Assert.DoesNotContain("help-callout", MarkdownConverter.ToHtml(markdown, Trusted));

        var html = MarkdownConverter.ToHtml(markdown, new TrustedMarkdownOptions { AlertTitles = new Dictionary<string, string> { ["NOTE"] = "Hinweis" } });
        Assert.Contains("<blockquote class=\"help-callout help-callout-note\" role=\"note\">", html);
        Assert.Contains("<p class=\"help-callout-title\">Hinweis</p>", html);
    }

    [Fact]
    public void Alerts_WithoutBodyTextKeepOnlyTheTitle()
    {
        var html = MarkdownConverter.ToHtml("> [!TIP]", new TrustedMarkdownOptions { AlertTitles = new Dictionary<string, string>() });

        Assert.Contains("<p class=\"help-callout-title\">Tip</p>", html);
        Assert.DoesNotContain("[!TIP]", html);
    }
}
