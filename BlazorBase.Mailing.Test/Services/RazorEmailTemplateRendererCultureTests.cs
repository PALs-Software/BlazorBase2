using BlazorBase.Mailing.Templates;
using BlazorBase.Mailing.Test.Infrastructure;
using System.Globalization;
using Xunit;

namespace BlazorBase.Mailing.Test.Services;

/// <summary>
/// Exercises the culture-aware <see cref="BlazorBase.Mailing.Services.RazorEmailTemplateRenderer"/>
/// <c>RenderAsync</c> overload end-to-end through a real <c>HtmlRenderer</c>, covering culture pinning
/// during render, restoration after render, and parity with the legacy single-parameter overload.
/// </summary>
public sealed class RazorEmailTemplateRendererCultureTests
{
    [Fact]
    public async Task PinsCulture_DuringRender()
    {
        var germanCulture = CultureInfo.GetCultureInfo("de");

        var html = await EmailRenderHarness.RenderAsync<CultureProbeComponent>(null, germanCulture);

        Assert.Contains("de", html);
    }

    [Fact]
    public async Task RestoresCulture_AfterRender()
    {
        var germanCulture = CultureInfo.GetCultureInfo("de");

        var pinnedHtml = await EmailRenderHarness.RenderAsync<CultureProbeComponent>(null, germanCulture);
        var restoredHtml = await EmailRenderHarness.RenderAsync<CultureProbeComponent>(null, culture: null);

        Assert.Contains("de", pinnedHtml);
        Assert.DoesNotContain("de", restoredHtml);
    }

    [Fact]
    public async Task NullCulture_MatchesLegacyOverload()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Subject"] = "Welcome aboard",
        };

        var legacyHtml = await EmailRenderHarness.RenderAsync<EmailLayoutBase>(parameters);
        var explicitNullCultureHtml = await EmailRenderHarness.RenderAsync<EmailLayoutBase>(parameters, culture: null);

        Assert.Equal(legacyHtml, explicitNullCultureHtml);
    }
}
