using BlazorBase.Components.Assistant;
using Xunit;

namespace BlazorBase.Components.Test.Assistant;

public sealed class AssistantMarkdownTests
{
    [Fact]
    public void Plain_Markdown_IsRenderedAsHtml()
    {
        var segment = Assert.Single(AssistantMarkdown.Split("Hello **world**"));

        Assert.False(segment.IsCode);
        Assert.Contains("<strong>world</strong>", segment.Html);
    }

    [Fact]
    public void EmptyInput_HasNoSegments()
    {
        Assert.Empty(AssistantMarkdown.Split(null));
        Assert.Empty(AssistantMarkdown.Split("  \n "));
    }

    [Fact]
    public void AFencedCodeBlock_BecomesItsOwnSegmentBetweenTheText()
    {
        var segments = AssistantMarkdown.Split("Before\n\n```csharp\nvar x = 1;\nConsole.WriteLine(x);\n```\n\nAfter");

        Assert.Equal(3, segments.Count);
        Assert.Contains("Before", segments[0].Html);
        Assert.True(segments[1].IsCode);
        Assert.Equal("csharp", segments[1].Language);
        Assert.Equal("var x = 1;\nConsole.WriteLine(x);", segments[1].Code!.Replace("\r\n", "\n"));
        Assert.Contains("After", segments[2].Html);
    }

    [Fact]
    public void AnUnclosedFence_WhileStreaming_IsAlreadyACodeSegment()
    {
        var segments = AssistantMarkdown.Split("Run:\n\n```bash\nnpm inst");

        Assert.True(segments[^1].IsCode);
        Assert.Equal("npm inst", segments[^1].Code);
    }

    [Fact]
    public void RawHtml_IsEscaped()
    {
        var html = AssistantMarkdown.Split("<script>alert(1)</script> and <img src=x onerror=alert(2)> <b>bold</b>")[0].Html;

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](JaVaScRiPt:alert(1))")]
    [InlineData("[x](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[x](vbscript:msgbox(1))")]
    [InlineData("[x](//evil.example/path)")]
    public void UnsafeLinks_BecomePlainText(string markdown)
    {
        var html = AssistantMarkdown.Split(markdown)[0].Html;

        Assert.DoesNotContain("href", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("x", html);
    }

    [Fact]
    public void ExternalLinks_OpenInANewTabWithNoopener()
    {
        var html = AssistantMarkdown.Split("[docs](https://example.com/a) and [page](/budgets)")[0].Html;

        Assert.Contains("href=\"https://example.com/a\"", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
        Assert.Contains("href=\"/budgets\"", html);
    }

    [Fact]
    public void Images_BecomeLinksSoNothingIsLoadedAutomatically()
    {
        var html = AssistantMarkdown.Split("![alt](https://tracker.example/pixel.png)")[0].Html;

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AttributeInjectionThroughMarkdown_StaysText()
    {
        var html = AssistantMarkdown.Split("text {onclick=alert(1)} [a](https://e.x){onmouseover=alert(2)}")[0].Html;

        Assert.DoesNotContain("onclick=\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onmouseover=\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACodeBlock_KeepsHtmlInsideAsRawCodeText()
    {
        var segments = AssistantMarkdown.Split("```html\n<script>alert(1)</script>\n```");

        var code = Assert.Single(segments);
        Assert.Equal("<script>alert(1)</script>", code.Code);
        Assert.Equal(string.Empty, code.Html);
    }
}
