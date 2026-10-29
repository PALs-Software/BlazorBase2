using System.Text.Json;
using BlazorBase.Components.Help;
using BlazorBase.Components.Layout;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.Help;

public sealed class HelpFieldTooltipsTests : ComponentsBunitTestContextBase
{
    private const string TooltipsScript = "./_content/BlazorBase.Components/js/helpTooltips.js";
    private const string PanelScript = "./_content/BlazorBase.Components/js/helpPanel.js";

    private readonly IHelpContentSource ContentSource = Substitute.For<IHelpContentSource>();
    private readonly IHelpTopicResolver TopicResolver = Substitute.For<IHelpTopicResolver>();
    private readonly IHelpFieldIndex FieldIndex = Substitute.For<IHelpFieldIndex>();
    private readonly HelpOptions Settings = new();
    private readonly BunitJSModuleInterop TooltipsModule;
    private readonly BunitJSModuleInterop PanelModule;

    public HelpFieldTooltipsTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(Substitute.For<IThemeService>());
        Services.AddSingleton(Substitute.For<ILanguageService>());
        Services.AddSingleton(ContentSource);
        Services.AddSingleton(TopicResolver);
        Services.AddSingleton(FieldIndex);
        Services.AddSingleton<IOptions<HelpOptions>>(Options.Create(Settings));
        AddAuthorization();
        TooltipsModule = JSInterop.SetupModule(TooltipsScript);
        PanelModule = JSInterop.SetupModule(PanelScript);
        TopicResolver.ResolveTopic(Arg.Any<string?>()).Returns("pages/blog");
        ContentSource.GetTopicAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HelpSourceDocument("getting-started", "en-US", "# Start\n\nWelcome."));
    }

    private IRenderedComponent<BaseLayout> RenderLayout(bool showHelp = true) =>
        Render<BaseLayout>(parameters => parameters
            .Add(p => p.AppTitle, "Shell")
            .Add(p => p.ShowHelp, showHelp)
            .AddChildContent("<p id=\"page\">Page</p>"));

    private IRenderedComponent<HelpFieldTooltips> RenderTooltips(string? culture = "en", Action<HelpAnchorTarget>? onOpen = null) =>
        Render<HelpFieldTooltips>(parameters => parameters
            .Add(p => p.CultureName, culture)
            .Add(p => p.OnOpenHelp, EventCallback.Factory.Create<HelpAnchorTarget>(this, target => onOpen?.Invoke(target))));

    [Fact]
    public void WithTheHelpOn_TheTooltipsAreEnabledWithTheLocalizedLink()
    {
        using var english = new EnglishCulture();

        var cut = RenderLayout();

        cut.WaitForAssertion(() => Assert.Single(TooltipsModule.Invocations["enableFieldTooltips"]));
        var options = JsonSerializer.Serialize(TooltipsModule.Invocations["enableFieldTooltips"].Single().Arguments[1]);
        Assert.Contains("\"moreLabel\":\"More in help\"", options);
        Assert.Contains("#base-main", options);
    }

    [Fact]
    public void WithGermanAsTheUiCulture_TheLinkIsGerman()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("de");

        try
        {
            RenderLayout();

            Assert.Contains("Mehr in der Hilfe", JsonSerializer.Serialize(TooltipsModule.Invocations["enableFieldTooltips"].Single().Arguments[1]));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void WhenTheOptionIsOff_NothingIsRegistered()
    {
        Settings.ShowFieldTooltips = false;

        var cut = RenderLayout();

        Assert.Empty(cut.FindComponents<HelpFieldTooltips>());
        Assert.Empty(TooltipsModule.Invocations);
    }

    [Fact]
    public void WithoutTheHelp_NothingIsRegistered()
    {
        var cut = RenderLayout(showHelp: false);

        Assert.Empty(cut.FindComponents<HelpFieldTooltips>());
        Assert.Empty(TooltipsModule.Invocations);
    }

    [Fact]
    public void TheOptionIsOnByDefault() => Assert.True(new HelpOptions().ShowFieldTooltips);

    [Fact]
    public async Task WithoutAFieldIndexService_TheLayoutStillWorksWithoutTooltips()
    {
        Services.Remove(Services.Single(service => service.ServiceType == typeof(IHelpFieldIndex)));

        var cut = RenderLayout();

        Assert.Empty(cut.FindComponents<HelpFieldTooltips>());
        Assert.Empty(TooltipsModule.Invocations);
        await cut.InvokeAsync(() => cut.Find(".base-help-toggle").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("aside.base-help-panel")));
    }

    [Fact]
    public async Task TheMoreInHelpLink_OpensThePanelAtTheFieldEntry()
    {
        var cut = RenderLayout();
        Assert.Empty(cut.FindAll("aside.base-help-panel"));

        await cut.InvokeAsync(() => cut.FindComponent<HelpFieldTooltips>().Instance.OpenHelpAsync("pages/blog", "field-title"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("aside.base-help-panel")));
        Assert.Equal("true", cut.Find(".base-help-toggle").GetAttribute("aria-expanded"));
        cut.WaitForAssertion(() => Assert.Contains(PanelModule.Invocations["scrollToHelpAnchor"], invocation => (string?)invocation.Arguments[1] == "field-title"));
    }

    [Fact]
    public async Task TheMoreInHelpLink_WorksAgainWhileThePanelIsOpen()
    {
        var cut = RenderLayout();
        await cut.InvokeAsync(() => cut.FindComponent<HelpFieldTooltips>().Instance.OpenHelpAsync("pages/blog", "field-title"));
        cut.WaitForAssertion(() => Assert.Single(PanelModule.Invocations["scrollToHelpAnchor"]));

        await cut.InvokeAsync(() => cut.FindComponent<HelpFieldTooltips>().Instance.OpenHelpAsync("pages/blog", "field-title"));

        cut.WaitForAssertion(() => Assert.Equal(2, PanelModule.Invocations["scrollToHelpAnchor"].Count));
    }

    [Theory]
    [InlineData("../secret", "field-title")]
    [InlineData("pages/blog", "")]
    [InlineData("pages/blog", "   ")]
    public async Task AnUnsafeTopicOrAnEmptyAnchor_IsIgnored(string topic, string anchor)
    {
        var raised = new List<HelpAnchorTarget>();
        var cut = RenderTooltips(onOpen: raised.Add);

        await cut.InvokeAsync(() => cut.Instance.OpenHelpAsync(topic, anchor));

        Assert.Empty(raised);
    }

    [Fact]
    public async Task AValidRequest_IsHandedToTheHost()
    {
        var raised = new List<HelpAnchorTarget>();
        var cut = RenderTooltips(onOpen: raised.Add);

        await cut.InvokeAsync(() => cut.Instance.OpenHelpAsync("pages/blog", "field-title"));

        Assert.Equal(new HelpAnchorTarget("pages/blog", "field-title"), Assert.Single(raised));
    }

    [Fact]
    public async Task TheScript_GetsTheIndexOfTheCurrentPage()
    {
        var data = new HelpFieldIndexData("pages/blog", [new HelpFieldTip("title", "Title", "The headline.", "field-title")]);
        FieldIndex.GetForPathAsync("blog", Arg.Any<CancellationToken>()).Returns(data);
        Services.GetRequiredService<NavigationManager>().NavigateTo("blog?x=1#top");
        var cut = RenderTooltips();

        var result = await cut.InvokeAsync(() => cut.Instance.GetFieldIndexAsync());

        Assert.Same(data, result);
    }

    [Fact]
    public async Task APageWithoutTips_GivesNull()
    {
        FieldIndex.GetForPathAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns((HelpFieldIndexData?)null);
        var cut = RenderTooltips();

        Assert.Null(await cut.InvokeAsync(() => cut.Instance.GetFieldIndexAsync()));
    }

    [Fact]
    public void ACultureChange_StartsTheScriptOverWithTheNewTexts()
    {
        var cut = RenderTooltips("en");
        Assert.Single(TooltipsModule.Invocations["enableFieldTooltips"]);

        cut.Render(parameters => parameters.Add(p => p.CultureName, "de"));

        cut.WaitForAssertion(() => Assert.Equal(2, TooltipsModule.Invocations["enableFieldTooltips"].Count));
    }

    [Fact]
    public void ARenderWithTheSameCulture_DoesNotStartTheScriptOver()
    {
        var cut = RenderTooltips("en");

        cut.Render(parameters => parameters.Add(p => p.CultureName, "en"));

        Assert.Single(TooltipsModule.Invocations["enableFieldTooltips"]);
    }

    [Fact]
    public void ANavigation_HidesTheTooltip()
    {
        RenderTooltips();

        Services.GetRequiredService<NavigationManager>().NavigateTo("elsewhere");

        Assert.NotEmpty(TooltipsModule.Invocations["hideFieldTooltip"]);
    }

    [Fact]
    public async Task Disposing_RemovesTheListenersAndStopsReactingToNavigation()
    {
        var cut = RenderTooltips();

        await DisposeComponentsAsync();

        Assert.Single(TooltipsModule.Invocations["disableFieldTooltips"]);

        Services.GetRequiredService<NavigationManager>().NavigateTo("after-dispose");

        Assert.Empty(TooltipsModule.Invocations["hideFieldTooltip"]);
        Assert.NotNull(cut);
    }
}
