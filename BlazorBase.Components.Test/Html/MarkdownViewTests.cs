using BlazorBase.Components.Html;
using BlazorBase.Components.Sanitization;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.Html;

public sealed class MarkdownViewTests : ComponentsBunitTestContextBase
{
    [Fact]
    public void WithoutSanitizer_RendersTheFormattedHtml()
    {
        var cut = Render<MarkdownView>(parameters => parameters.Add(view => view.Value, "Hello **world**"));

        Assert.Contains("<strong>world</strong>", cut.Markup);
        Assert.Contains("markdown-view", cut.Find("div").ClassName);
    }

    [Fact]
    public void WithSanitizer_AppliesTheHostPolicyOnTop()
    {
        var sanitizer = Substitute.For<IHtmlSanitizer>();
        sanitizer.Sanitize(Arg.Any<string>()).Returns("<p>sanitized</p>");
        Services.AddSingleton(sanitizer);

        var cut = Render<MarkdownView>(parameters => parameters.Add(view => view.Value, "Hello **world**"));

        sanitizer.Received(1).Sanitize(Arg.Is<string>(html => html.Contains("<strong>world</strong>")));
        Assert.Contains("sanitized", cut.Markup);
        Assert.DoesNotContain("<strong>", cut.Markup);
    }

    [Fact]
    public void SameValue_IsNotConvertedAgain()
    {
        var sanitizer = Substitute.For<IHtmlSanitizer>();
        sanitizer.Sanitize(Arg.Any<string>()).Returns(call => call.Arg<string>());
        Services.AddSingleton(sanitizer);
        var cut = Render<MarkdownView>(parameters => parameters.Add(view => view.Value, "text"));

        cut.Render(parameters => parameters.Add(view => view.Value, "text").Add(view => view.Class, "other"));
        cut.Render(parameters => parameters.Add(view => view.Value, "changed"));

        sanitizer.Received(2).Sanitize(Arg.Any<string>());
        Assert.Contains("changed", cut.Markup);
    }

    [Fact]
    public void AppliesExtraClasses()
    {
        var cut = Render<MarkdownView>(parameters => parameters.Add(view => view.Value, "x").Add(view => view.Class, "chat-markdown"));

        Assert.Contains("chat-markdown", cut.Find("div").ClassName);
    }
}
