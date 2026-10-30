using BlazorBase.Components.Help;
using BlazorBase.Components.Layout;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.Layout;

public sealed class BaseLayoutHelpTests : ComponentsBunitTestContextBase
{
    private readonly IHelpContentSource ContentSource = Substitute.For<IHelpContentSource>();

    public BaseLayoutHelpTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(Substitute.For<IThemeService>());
        Services.AddSingleton(Substitute.For<ILanguageService>());
        Services.AddSingleton(ContentSource);
        Services.AddSingleton(Substitute.For<IHelpTopicResolver>());
        Services.AddSingleton<IOptions<HelpOptions>>(Options.Create(new HelpOptions()));
        AddAuthorization();
        ContentSource.GetTopicAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("getting-started", "en-US", "# Start\n\nWelcome."));
    }

    private IRenderedComponent<BaseLayout> RenderLayout(bool showHelp = true) =>
        Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.ShowHelp, showHelp)
            .AddChildContent("<p id=\"page\">Page</p>"));

    [Fact]
    public void ByDefault_NoHelpButtonAndNoPanelAreRendered()
    {
        var cut = RenderLayout(showHelp: false);

        Assert.Empty(cut.FindAll(".base-help-toggle"));
        Assert.Empty(cut.FindAll("aside.base-help-panel"));
    }

    [Fact]
    public void ShowHelp_RendersALabelledButtonLeftOfTheThemeToggleAndTheSettingsButton()
    {
        using var english = new EnglishCulture();
        var cut = RenderLayout();

        var buttons = cut.FindAll(".base-header-end .base-header-control").Select(button => button.ClassList.Contains("base-help-toggle") ? "help" : button.ClassList.Contains("base-settings-toggle") ? "settings" : "other").ToList();
        var help = cut.Find(".base-help-toggle");

        Assert.True(buttons.IndexOf("help") >= 0 && buttons.IndexOf("help") < buttons.IndexOf("settings"));
        Assert.Equal("Help", help.GetAttribute("aria-label"));
        Assert.Equal("base-help", help.GetAttribute("aria-controls"));
        Assert.Equal("false", help.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void HeaderEnd_RendersHeaderContent_Help_Theme_Settings_HeaderEndContent_InThisOrder()
    {
        var cut = Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.ShowHelp, true)
            .Add(p => p.HeaderContent, builder =>
            {
                builder.AddMarkupContent(0, "<span class=\"test-start\"></span>");
            })
            .Add(p => p.HeaderEndContent, builder =>
            {
                builder.AddMarkupContent(0, "<span class=\"test-end\"></span>");
            })
            .AddChildContent("<p id=\"page\">Page</p>"));

        var html = cut.Find(".base-header-end").InnerHtml;
        var start = html.IndexOf("test-start", StringComparison.Ordinal);
        var help = html.IndexOf("base-help-toggle", StringComparison.Ordinal);
        var theme = html.IndexOf("base-header-theme", StringComparison.Ordinal);
        var settings = html.IndexOf("base-settings-toggle", StringComparison.Ordinal);
        var end = html.IndexOf("test-end", StringComparison.Ordinal);

        Assert.True(start >= 0 && start < help);
        Assert.True(help < theme);
        Assert.True(theme < settings);
        Assert.True(settings < end);
        Assert.Single(cut.FindAll(".base-header-end > .test-end"));
        Assert.Equal("test-end", cut.Find(".base-header-end").LastElementChild!.ClassName);
    }

    [Fact]
    public void WithoutHeaderEndContent_TheSettingsButtonIsTheLastHeaderControl()
    {
        var cut = RenderLayout();

        Assert.Contains("base-settings-toggle", cut.Find(".base-header-end").LastElementChild!.ClassName);
    }

    [Fact]
    public void TheButton_OpensThePanelAndTogglesItClosed()
    {
        var cut = RenderLayout();

        cut.Find(".base-help-toggle").Click();

        Assert.Equal("true", cut.Find(".base-help-toggle").GetAttribute("aria-expanded"));
        cut.WaitForAssertion(() => Assert.Contains("Welcome.", cut.Find("aside.base-help-panel").InnerHtml));

        cut.Find(".base-help-toggle").Click();

        Assert.Empty(cut.FindAll("aside.base-help-panel"));
        Assert.Equal("false", cut.Find(".base-help-toggle").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task EscapeAndTheCloseButton_CloseThePanel()
    {
        var cut = RenderLayout();
        cut.Find(".base-help-toggle").Click();

        await cut.InvokeAsync(() => cut.Find("aside.base-help-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" }));
        Assert.Empty(cut.FindAll("aside.base-help-panel"));

        cut.Find(".base-help-toggle").Click();
        await cut.InvokeAsync(() => cut.Find(".base-help-close").Click());
        Assert.Empty(cut.FindAll("aside.base-help-panel"));
    }

    [Fact]
    public void OpeningTheSettings_ClosesTheHelp()
    {
        var cut = RenderLayout();
        cut.Find(".base-help-toggle").Click();

        cut.Find(".base-settings-toggle").Click();

        Assert.Empty(cut.FindAll("aside.base-help-panel"));
        Assert.Single(cut.FindAll("aside.base-settings-panel"));
    }

    [Fact]
    public void OpeningTheHelp_ClosesTheSettings()
    {
        var cut = RenderLayout();
        cut.Find(".base-settings-toggle").Click();

        cut.Find(".base-help-toggle").Click();

        Assert.Empty(cut.FindAll("aside.base-settings-panel"));
        Assert.Single(cut.FindAll("aside.base-help-panel"));
    }

    [Fact]
    public void AsASheetOnANarrowViewport_ThePageBehindIsInert()
    {
        var cut = RenderLayout();

        cut.Find(".base-help-toggle").Click();

        Assert.True(cut.Find("main#base-main").HasAttribute("inert"));
        Assert.Contains("overlay", cut.Find("aside.base-help-panel").ClassList);
    }

    [Fact]
    public void DockedOnAWideViewport_ThePageStaysUsable()
    {
        JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js").Setup<bool>("watchWide", _ => true).SetResult(true);

        var cut = RenderLayout();
        cut.Find(".base-help-toggle").Click();

        Assert.False(cut.Find("main#base-main").HasAttribute("inert"));
        Assert.Contains("docked", cut.Find("aside.base-help-panel").ClassList);
        Assert.Null(cut.Find(".base-help-toggle").GetAttribute("aria-haspopup"));
    }
}
