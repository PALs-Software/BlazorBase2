using BlazorBase.Components.Html;
using Xunit;

namespace BlazorBase.Components.Test.Html;

public sealed class MarkdownConverterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInput_RendersNothing(string? markdown)
        => Assert.Equal(string.Empty, MarkdownConverter.ToHtml(markdown));

    [Fact]
    public void RendersTheCommonElements()
    {
        var html = MarkdownConverter.ToHtml("## Title\n\nSome **bold**, *italic* and ~~old~~ text.\n\n- one\n- two\n\n1. first\n\n> quoted\n\n---\n\n`inline`");

        Assert.Contains("<h2", html);
        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("<em>italic</em>", html);
        Assert.Contains("<del>old</del>", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<ol>", html);
        Assert.Contains("<blockquote>", html);
        Assert.Contains("<hr", html);
        Assert.Contains("<code>inline</code>", html);
    }

    [Fact]
    public void CodeBlocks_KeepTheirLanguageAndEscapeTheirContent()
    {
        var html = MarkdownConverter.ToHtml("```csharp\nif (a < b) Console.WriteLine(\"<x>\");\n```");

        Assert.Contains("<pre><code class=\"language-csharp\">", html);
        Assert.Contains("a &lt; b", html);
        Assert.Contains("&quot;&lt;x&gt;&quot;", html);
    }

    [Fact]
    public void PipeTables_BecomeTables()
    {
        var html = MarkdownConverter.ToHtml("| Name | Value |\n|---|---|\n| a | 1 |");

        Assert.Contains("<table>", html);
        Assert.Contains("<th>Name</th>", html);
        Assert.Contains("<td>1</td>", html);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("before <img src=x onerror=alert(1)> after")]
    [InlineData("<div onclick=\"alert(1)\">block</div>")]
    public void RawHtml_IsEscapedNotRendered(string markdown)
    {
        var html = MarkdownConverter.ToHtml(markdown);

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<div", html);
        Assert.Contains("&lt;", html);
    }

    [Theory]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("[click](JAVASCRIPT:alert(1))")]
    [InlineData("[click](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[click](vbscript:msgbox)")]
    [InlineData("[click](<java\tscript:alert(1)>)")]
    [InlineData("[click](jav&#x09;ascript:alert(1))")]
    public void DangerousLinks_BecomePlainText(string markdown)
    {
        var html = MarkdownConverter.ToHtml(markdown);

        Assert.DoesNotContain("<a", html);
        Assert.DoesNotContain("href", html);
        Assert.Contains("click", html);
    }

    [Fact]
    public void DangerousAutolinks_BecomePlainText()
    {
        var html = MarkdownConverter.ToHtml("<javascript:alert(1)>");

        Assert.DoesNotContain("<a", html);
    }

    [Theory]
    [InlineData("[docs](https://example.com/docs)", "href=\"https://example.com/docs\"")]
    [InlineData("[mail](mailto:dev@example.com)", "href=\"mailto:dev@example.com\"")]
    [InlineData("[item](/work-items/42)", "href=\"/work-items/42\"")]
    [InlineData("[section](#setup)", "href=\"#setup\"")]
    [InlineData("see https://example.com/page now", "href=\"https://example.com/page\"")]
    public void SafeLinks_Survive(string markdown, string expected)
        => Assert.Contains(expected, MarkdownConverter.ToHtml(markdown));

    [Fact]
    public void Images_BecomeLinks_SoNothingIsFetched()
    {
        var html = MarkdownConverter.ToHtml("![diagram](https://tracker.example.com/pixel.png)");

        Assert.DoesNotContain("<img", html);
        Assert.Contains("href=\"https://tracker.example.com/pixel.png\"", html);
        Assert.Contains(">diagram</a>", html);
    }

    [Fact]
    public void GenericAttributes_AreNotInterpreted()
    {
        var html = MarkdownConverter.ToHtml("# Title {onclick=alert(1)}");

        Assert.DoesNotContain("onclick=\"", html);
    }
}
