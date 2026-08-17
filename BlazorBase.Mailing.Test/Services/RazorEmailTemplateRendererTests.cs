using BlazorBase.Mailing.Templates;
using BlazorBase.Mailing.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Mailing.Test.Services;

/// <summary>
/// Exercises <see cref="BlazorBase.Mailing.Services.RazorEmailTemplateRenderer"/> by rendering the
/// shipped <see cref="EmailLayoutBase"/> template through a real <c>HtmlRenderer</c> and asserting on
/// the produced HTML — covering parameter binding, conditional markup, and defaults.
/// </summary>
public sealed class RazorEmailTemplateRendererTests
{
    [Fact]
    public async Task RendersSubject_IntoTitle()
    {
        var html = await EmailRenderHarness.RenderAsync<EmailLayoutBase>(new Dictionary<string, object?>
        {
            ["Subject"] = "Welcome aboard",
        });

        Assert.Contains("<title>Welcome aboard</title>", html);
    }

    [Fact]
    public async Task RendersBrandName_InHeader_WhenNoHeaderFragment()
    {
        var html = await EmailRenderHarness.RenderAsync<EmailLayoutBase>(new Dictionary<string, object?>
        {
            ["BrandName"] = "DevPortal",
        });

        Assert.Contains("DevPortal", html);
        Assert.Contains("class=\"header\"", html);
    }

    [Fact]
    public async Task RendersLanguageAttribute()
    {
        var html = await EmailRenderHarness.RenderAsync<EmailLayoutBase>(new Dictionary<string, object?>
        {
            ["Language"] = "de",
        });

        Assert.Contains("lang=\"de\"", html);
    }

    [Fact]
    public async Task RendersPreheader_WhenProvided()
    {
        var html = await EmailRenderHarness.RenderAsync<EmailLayoutBase>(new Dictionary<string, object?>
        {
            ["PreheaderText"] = "Sneak peek inside",
        });

        Assert.Contains("class=\"preheader\"", html);
        Assert.Contains("Sneak peek inside", html);
    }

    [Fact]
    public async Task OmitsPreheader_WhenBlank()
    {
        var html = await EmailRenderHarness.RenderAsync<EmailLayoutBase>();

        Assert.DoesNotContain("class=\"preheader\"", html);
    }

    [Fact]
    public async Task UsesDefaults_WhenNoParametersProvided()
    {
        var html = await EmailRenderHarness.RenderAsync<EmailLayoutBase>();

        Assert.Contains("lang=\"en\"", html);
        Assert.Contains("App", html);
    }
}
