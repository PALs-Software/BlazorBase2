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

    /// <summary>
    /// <see cref="BlazorBase.Mailing.Globalization.CultureScope.Dispose"/> itself is covered by
    /// <see cref="BlazorBase.Mailing.Test.Globalization.CultureScopeTests"/> — the dispatcher's
    /// captured execution context hides its effect here, so this test only shows that an unpinned
    /// render observes the caller's ambient culture and does not leak a pinned culture back to it.
    /// </summary>
    [Fact]
    public async Task AnUnpinnedRender_UsesTheAmbientCulture_AndDoesNotLeakBackToTheCaller()
    {
        var germanCulture = CultureInfo.GetCultureInfo("de");
        var ambientCulture = CultureInfo.GetCultureInfo("en-US");

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = ambientCulture;
        CultureInfo.CurrentUICulture = ambientCulture;
        try
        {
            var pinnedHtml = await EmailRenderHarness.RenderAsync<CultureProbeComponent>(null, germanCulture);
            var restoredHtml = await EmailRenderHarness.RenderAsync<CultureProbeComponent>(null, culture: null);

            Assert.Contains("de", pinnedHtml);
            Assert.Contains("en", restoredHtml);
            Assert.Equal(ambientCulture, CultureInfo.CurrentCulture);
            Assert.Equal(ambientCulture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
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
