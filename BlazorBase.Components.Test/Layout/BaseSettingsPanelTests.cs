using BlazorBase.Components.Layout;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.Components.Test.Layout;

public sealed class BaseSettingsPanelTests : ComponentsBunitTestContextBase
{
    private readonly IThemeService ThemeService = Substitute.For<IThemeService>();
    private readonly ILanguageService LanguageService = Substitute.For<ILanguageService>();

    public BaseSettingsPanelTests()
    {
        LanguageService.CurrentLanguage.Returns("en");
        Services.AddLocalization();
        Services.AddSingleton(ThemeService);
        Services.AddSingleton(LanguageService);
    }

    private IRenderedComponent<BaseSettingsPanel> RenderPanel(Action<ComponentParameterCollectionBuilder<BaseSettingsPanel>>? configure = null) =>
        Render<BaseSettingsPanel>(parameters =>
        {
            parameters.Add(p => p.IsOpen, true);
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Closed_RendersNothing()
    {
        var cut = Render<BaseSettingsPanel>(parameters => parameters.Add(p => p.IsOpen, false));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void Open_IsAModalDialogLabelledByItsTitle()
    {
        var cut = RenderPanel();

        var panel = cut.Find("aside.base-settings-panel");
        Assert.Equal("dialog", panel.GetAttribute("role"));
        Assert.Equal("true", panel.GetAttribute("aria-modal"));
        Assert.Equal("base-settings-title", panel.GetAttribute("aria-labelledby"));
        Assert.NotNull(cut.Find("#base-settings-title"));
    }

    [Fact]
    public void Open_OffersTheDefaultLanguagesAndAllThreeThemes()
    {
        var cut = RenderPanel();

        var radios = cut.FindAll("fluent-radio").Select(radio => radio.TextContent.Trim()).ToList();

        Assert.Equal(5, radios.Count);
        Assert.Equal(["English", "Deutsch"], radios.Take(2));
    }

    [Fact]
    public void AnEmptyLanguageList_HidesTheLanguageSection()
    {
        var cut = RenderPanel(parameters => parameters.Add(p => p.LanguageOptions, []));

        Assert.Equal(3, cut.FindAll("fluent-radio").Count);
    }

    [Fact]
    public async Task ChoosingALanguage_WithoutAHostHandler_ChangesTheLanguageDirectly()
    {
        var cut = RenderPanel();

        await cut.InvokeAsync(() => cut.FindAll("fluent-radio-group")[0].TriggerEventAsync("onradiogroupchange", new ChangeEventArgs { Value = "de" }));

        await LanguageService.Received(1).SetLanguageAsync("de");
    }

    [Fact]
    public async Task ChoosingALanguage_WithAHostHandler_RaisesItInsteadOfChangingTheLanguage()
    {
        var chosen = new List<string>();
        var cut = RenderPanel(parameters => parameters
            .Add(p => p.OnLanguageSelected, EventCallback.Factory.Create<string>(this, code => chosen.Add(code))));

        await cut.InvokeAsync(() => cut.FindAll("fluent-radio-group")[0].TriggerEventAsync("onradiogroupchange", new ChangeEventArgs { Value = "de" }));

        Assert.Equal(["de"], chosen);
        await LanguageService.DidNotReceive().SetLanguageAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ChoosingTheCurrentLanguage_DoesNothing()
    {
        var chosen = 0;
        var cut = RenderPanel(parameters => parameters
            .Add(p => p.OnLanguageSelected, EventCallback.Factory.Create<string>(this, _ => chosen++)));

        await cut.InvokeAsync(() => cut.FindAll("fluent-radio-group")[0].TriggerEventAsync("onradiogroupchange", new ChangeEventArgs { Value = "en" }));

        Assert.Equal(0, chosen);
    }

    [Fact]
    public async Task ChoosingATheme_AppliesItThroughTheThemeService()
    {
        var cut = RenderPanel();

        await cut.InvokeAsync(() => cut.FindAll("fluent-radio-group")[1].TriggerEventAsync("onradiogroupchange", new ChangeEventArgs { Value = "Dark" }));

        await ThemeService.Received(1).SetThemeAsync(ThemePreference.Dark);
    }

    [Fact]
    public async Task CloseButtonAndEscape_AskTheHostToClose()
    {
        var closed = 0;
        var cut = RenderPanel(parameters => parameters.Add(p => p.OnClose, EventCallback.Factory.Create(this, () => closed++)));

        await cut.InvokeAsync(() => cut.Find("fluent-button.base-settings-close").Click());
        await cut.InvokeAsync(() => cut.Find("aside").KeyDown(new KeyboardEventArgs { Key = "Escape" }));
        await cut.InvokeAsync(() => cut.Find(".base-settings-overlay").Click());

        Assert.Equal(3, closed);
    }

    [Fact]
    public async Task HostSections_AreRenderedWithAContextThatClosesThePanel()
    {
        var closed = 0;
        var cut = RenderPanel(parameters => parameters
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => closed++))
            .Add(p => p.ChildContent, context => builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "id", "host-action");
                builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, context.CloseAsync));
                builder.CloseElement();
            }));

        await cut.InvokeAsync(() => cut.Find("#host-action").Click());

        Assert.Equal(1, closed);
    }

    [Fact]
    public void Opening_MovesTheFocusIntoThePanelAndKeepsItThere()
    {
        var module = JSInterop.SetupModule("./_content/BlazorBase.Components/js/layout.js");
        module.SetupVoid("trapFocus", _ => true).SetVoidResult();

        RenderPanel();

        Assert.Single(module.Invocations["trapFocus"]);
    }
}
