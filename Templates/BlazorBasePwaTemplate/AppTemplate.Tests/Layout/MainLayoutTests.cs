using AppTemplate.Client.Layout;
using AppTemplate.Tests.Infrastructure;
using BlazorBase.Components.Services;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace AppTemplate.Tests.Layout;

public class MainLayoutTests : BunitContext
{
    public MainLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazorBaseFluentUI();
        Services.AddLocalization();
        Services.AddSingleton<IThemeService, StubThemeService>();
        Services.AddSingleton<ILanguageService, StubLanguageService>();
        Services.AddBlazorBaseComponents();

        Authorization = this.AddAuthorization();
        Authorization.SetAuthorized("test-user");
    }

    private readonly BunitAuthorizationContext Authorization;

    [Fact]
    public void Render_AnonymousVisitor_SeesNoCommandPalette()
    {
        Authorization.SetNotAuthorized();

        var component = Render<MainLayout>();

        Assert.DoesNotContain("has-command-palette", component.Find("header").ClassList);
        Assert.Empty(component.FindAll("button.base-command-search"));
    }

    [Fact]
    public void Render_ShowsBrandLinkToTheAppRootWithAccessibleName()
    {
        var component = Render<MainLayout>();

        var brand = component.Find("a.app-brand");
        var localizer = Services.GetRequiredService<IStringLocalizer<AppBrand>>();
        Assert.Equal(localizer["HomeLabel"].Value, brand.GetAttribute("aria-label"));
        Assert.Equal("./", brand.GetAttribute("href"));
        Assert.Equal("icon.svg", brand.QuerySelector("img.app-brand-mark")?.GetAttribute("src"));
        Assert.Equal("AppTemplate", brand.QuerySelector(".app-brand-wordmark")?.TextContent);
    }

    [Fact]
    public void Render_ShowsBrandAtTheStartOfTheHeaderInsteadOfThePlainTitle()
    {
        var component = Render<MainLayout>();

        Assert.NotNull(component.Find(".base-header-start .base-header-brand a.app-brand"));
        Assert.Empty(component.FindAll(".base-header-title"));
        Assert.Contains("brand-start", component.Find("header").ClassList);
    }

    [Fact]
    public void Render_ShowsCommandPaletteSearchButtonInTheHeader()
    {
        var component = Render<MainLayout>();

        Assert.Contains("has-command-palette", component.Find("header").ClassList);
        Assert.NotNull(component.Find("header .base-header-search button.base-command-search"));
        Assert.NotEmpty(component.FindAll("header .base-command-search-icon"));
    }
}
