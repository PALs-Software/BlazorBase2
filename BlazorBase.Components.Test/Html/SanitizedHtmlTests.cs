using BlazorBase.Components.Sanitization;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using SanitizedHtmlComponent = BlazorBase.Components.Html.SanitizedHtml;

namespace BlazorBase.CRUD.Test.Components;

public class SanitizedHtmlTests : ComponentsBunitTestContextBase
{
    private const string MaliciousValue = "<script>alert(1)</script><b>ok</b>";
    private const string SanitizedValue = "<b>ok</b>";

    [Fact]
    public void RendersSanitizerOutput_WhenHostSanitizerIsRegistered()
    {
        var sanitizer = Substitute.For<IHtmlSanitizer>();
        sanitizer.Sanitize(MaliciousValue).Returns(SanitizedValue);
        Services.AddSingleton(sanitizer);

        var cut = Render<SanitizedHtmlComponent>(parameters => parameters
            .Add(p => p.Value, MaliciousValue));

        sanitizer.Received(1).Sanitize(MaliciousValue);
        Assert.Contains(SanitizedValue, cut.Markup);
        Assert.DoesNotContain("<script>", cut.Markup);
    }

    [Fact]
    public void HtmlEncodesValue_WhenNoHostSanitizerIsRegistered()
    {
        var cut = Render<SanitizedHtmlComponent>(parameters => parameters
            .Add(p => p.Value, MaliciousValue));

        Assert.Contains("&lt;script&gt;", cut.Markup);
        Assert.DoesNotContain("<script>", cut.Markup);
    }
}
