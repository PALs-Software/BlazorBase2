using System.Globalization;
using System.Security.Claims;
using BlazorBase.Components.Services;
using BlazorBase.User.Components.UserMenu;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;
using NSubstitute;
using Xunit;
using IThemeService = BlazorBase.Components.Services.IThemeService;

namespace BlazorBase.User.Test.Components;

/// <summary>
/// The user menu opens from the avatar button, shows who is signed in, switches language and theme through the
/// framework services, signs out through the framework sign-out path and closes the way a menu is expected to.
/// </summary>
public sealed class BaseUserMenuTests : UserBunitTestContextBase
{
    private const string ModulePath = "./_content/BlazorBase.User/js/userMenu.js";

    private readonly BunitJSModuleInterop Module;
    private readonly ILanguageService LanguageService = Substitute.For<ILanguageService>();
    private readonly IThemeService ThemeService = Substitute.For<IThemeService>();
    private readonly IUserLanguagePreferenceSync LanguagePreferenceSync = Substitute.For<IUserLanguagePreferenceSync>();
    private readonly IUserSignOutService SignOutService = Substitute.For<IUserSignOutService>();
    private readonly BunitAuthorizationContext Authorization;
    private readonly CultureInfo PreviousUiCulture = CultureInfo.CurrentUICulture;

    public BaseUserMenuTests()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        Services.AddLocalization();
        Services.AddSingleton(LanguageService);
        Services.AddSingleton(ThemeService);
        Services.AddSingleton(LanguagePreferenceSync);
        Services.AddSingleton(SignOutService);

        LanguageService.CurrentLanguage.Returns("en");
        ThemeService.CurrentTheme.Returns(ThemePreference.System);

        Module = JSInterop.SetupModule(ModulePath);
        Module.SetupVoid("attach", _ => true).SetVoidResult();
        Module.SetupVoid("detach").SetVoidResult();
        Module.SetupVoid("focusItem", _ => true).SetVoidResult();

        Authorization = AddAuthorization();
        Authorization.SetAuthorized("ada@example.com");
        Authorization.SetClaims(
            new Claim("displayName", "Ada Lovelace"),
            new Claim(ClaimTypes.Email, "ada@example.com"));
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentUICulture = PreviousUiCulture;
        base.Dispose(disposing);
    }

    private IRenderedComponent<BaseUserMenu> RenderMenu(Action<ComponentParameterCollectionBuilder<BaseUserMenu>>? configure = null) =>
        Render<BaseUserMenu>(parameters => configure?.Invoke(parameters));

    private static AngleSharp.Dom.IElement ToggleOf(IRenderedComponent<BaseUserMenu> menu) => menu.Find("button.base-user-menu-toggle");

    private static void Open(IRenderedComponent<BaseUserMenu> menu) => ToggleOf(menu).Click();

    [Fact]
    public void Anonymous_RendersNothing()
    {
        Authorization.SetNotAuthorized();

        var menu = RenderMenu();

        Assert.Empty(menu.FindAll(".base-user-menu"));
    }

    [Fact]
    public void Closed_RendersAnAccessibleAvatarButtonWithTheInitialsAndNoMenu()
    {
        var menu = RenderMenu();

        var toggle = ToggleOf(menu);
        Assert.Equal("button", toggle.GetAttribute("type"));
        Assert.Equal("menu", toggle.GetAttribute("aria-haspopup"));
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Null(toggle.GetAttribute("aria-controls"));
        Assert.Equal("Account menu of Ada Lovelace", toggle.GetAttribute("aria-label"));
        Assert.Contains("base-header-control", toggle.ClassList);
        Assert.Equal("AL", menu.Find(".base-user-menu-avatar").TextContent);
        Assert.Empty(menu.FindAll("[role=menu]"));
    }

    [Fact]
    public void Click_OpensTheMenuWithTheNameAndTheAccount_AndFocusesTheFirstEntry()
    {
        var menu = RenderMenu();

        Open(menu);

        var toggle = ToggleOf(menu);
        var list = menu.Find("[role=menu]");
        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        Assert.Equal(list.Id, toggle.GetAttribute("aria-controls"));
        Assert.Equal("Account menu", list.GetAttribute("aria-label"));
        Assert.Equal("Ada Lovelace", menu.Find(".base-user-menu-name").TextContent);
        Assert.Equal("ada@example.com", menu.Find(".base-user-menu-account").TextContent);
        Module.VerifyInvoke("attach");
        Assert.Equal("first", Module.VerifyInvoke("focusItem").Arguments[1]);
    }

    [Fact]
    public void ClickOnTheOpenToggle_ClosesTheMenu()
    {
        var menu = RenderMenu();
        Open(menu);

        Open(menu);

        Assert.Equal("false", ToggleOf(menu).GetAttribute("aria-expanded"));
        Assert.Empty(menu.FindAll("[role=menu]"));
        Module.VerifyInvoke("detach");
    }

    [Fact]
    public void Escape_ClosesTheMenuAndReturnsTheFocusToTheButton()
    {
        var menu = RenderMenu();
        Open(menu);

        menu.Find(".base-user-menu").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(menu.FindAll("[role=menu]"));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void ArrowDownOnTheButton_OpensTheMenuAtTheFirstEntry()
    {
        var menu = RenderMenu();

        ToggleOf(menu).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.NotNull(menu.Find("[role=menu]"));
        Assert.Equal("first", Module.VerifyInvoke("focusItem").Arguments[1]);
    }

    [Fact]
    public void ArrowUpOnTheButton_OpensTheMenuAtTheLastEntry()
    {
        var menu = RenderMenu();

        ToggleOf(menu).KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.NotNull(menu.Find("[role=menu]"));
        Assert.Equal("last", Module.VerifyInvoke("focusItem").Arguments[1]);
    }

    [Fact]
    public void OtherKeysOnTheButton_DoNotOpenTheMenu()
    {
        var menu = RenderMenu();

        ToggleOf(menu).KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Empty(menu.FindAll("[role=menu]"));
    }

    [Fact]
    public void BackdropClick_ClosesTheMenuWithoutMovingTheFocus()
    {
        var menu = RenderMenu();
        Open(menu);

        menu.Find(".base-user-menu-backdrop").Click();

        Assert.Empty(menu.FindAll("[role=menu]"));
        Assert.DoesNotContain(JSInterop.Invocations, invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public async Task OutsidePressReportedByTheBrowser_ClosesTheMenu()
    {
        var menu = RenderMenu();
        Open(menu);

        await menu.InvokeAsync(() => menu.Instance.OnDismissRequested(false));

        Assert.Empty(menu.FindAll("[role=menu]"));
    }

    [Fact]
    public void Navigation_ClosesTheMenu()
    {
        var menu = RenderMenu();
        Open(menu);

        Services.GetRequiredService<NavigationManager>().NavigateTo("notes");

        menu.WaitForAssertion(() => Assert.Empty(menu.FindAll("[role=menu]")));
    }

    [Fact]
    public void Languages_AreListedAsRadioEntriesWithTheCurrentOneChecked()
    {
        var menu = RenderMenu();
        Open(menu);

        var entries = menu.FindAll(".base-user-menu-languages [role=menuitemradio]");
        Assert.Equal(["English", "Deutsch"], entries.Select(entry => entry.TextContent.Trim()));
        Assert.Equal(["true", "false"], entries.Select(entry => entry.GetAttribute("aria-checked")));
        Assert.Equal("Language", menu.Find(".base-user-menu-languages").GetAttribute("aria-label"));
    }

    [Fact]
    public void ChoosingALanguage_StoresItOnTheAccountAndSwitchesTheLanguage()
    {
        var menu = RenderMenu();
        Open(menu);

        menu.Find("[data-language=de]").Click();

        Received.InOrder(() =>
        {
            LanguagePreferenceSync.SaveAsync("de");
            LanguageService.SetLanguageAsync("de");
        });
        Assert.Empty(menu.FindAll("[role=menu]"));
    }

    [Fact]
    public void ChoosingTheCurrentLanguage_ChangesNothing()
    {
        var menu = RenderMenu();
        Open(menu);

        menu.Find("[data-language=en]").Click();

        LanguagePreferenceSync.DidNotReceive().SaveAsync(Arg.Any<string>());
        LanguageService.DidNotReceive().SetLanguageAsync(Arg.Any<string>());
    }

    [Fact]
    public void Languages_TakesTheLanguagesOfTheApp()
    {
        var menu = RenderMenu(parameters => parameters.Add(p => p.Languages, ["de", "fr", "en"]));
        Open(menu);

        var codes = menu.FindAll(".base-user-menu-languages [role=menuitemradio]").Select(entry => entry.GetAttribute("data-language"));
        Assert.Equal(["de", "fr", "en"], codes);
    }

    [Fact]
    public void ThemeChoice_ShowsSystemLightAndDarkWithTheCurrentOneChecked()
    {
        ThemeService.CurrentTheme.Returns(ThemePreference.Light);

        var menu = RenderMenu();
        Open(menu);

        var entries = menu.FindAll(".base-user-menu-themes [role=menuitemradio]");
        Assert.Equal(["System", "Light", "Dark"], entries.Select(entry => entry.TextContent.Trim()));
        Assert.Equal(["false", "true", "false"], entries.Select(entry => entry.GetAttribute("aria-checked")));
    }

    [Fact]
    public void ChoosingATheme_SetsItThroughTheThemeService()
    {
        var menu = RenderMenu();
        Open(menu);

        menu.Find("[data-theme-option=Dark]").Click();

        ThemeService.Received(1).SetThemeAsync(ThemePreference.Dark);
        Assert.Empty(menu.FindAll("[role=menu]"));
    }

    [Fact]
    public void SignOut_RunsTheFrameworkSignOutAndNavigatesToTheLoginPage()
    {
        SignOutService.SignOutAsync().Returns(SignOutResult.SignedOut);
        var menu = RenderMenu();
        Open(menu);

        menu.Find(".base-user-menu-sign-out").Click();

        SignOutService.Received(1).SignOutAsync();
        Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Empty(menu.FindAll(".base-user-menu-error"));
    }

    [Fact]
    public void SignOut_WithoutServerRevocation_StillNavigatesToTheLoginPage()
    {
        SignOutService.SignOutAsync().Returns(SignOutResult.SignedOutWithoutServerRevocation);
        var menu = RenderMenu();
        Open(menu);

        menu.Find(".base-user-menu-sign-out").Click();

        Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void SignOut_Failed_StaysOnThePageShowsTheMessageAndKeepsTheMenuUsable()
    {
        SignOutService.SignOutAsync().Returns(SignOutResult.Failed);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var uriBefore = navigationManager.Uri;
        var menu = RenderMenu();
        Open(menu);

        menu.Find(".base-user-menu-sign-out").Click();

        Assert.Equal(uriBefore, navigationManager.Uri);
        var alert = menu.Find(".base-user-menu-error");
        Assert.Equal("alert", alert.GetAttribute("role"));
        Assert.Equal("Signing out failed – please try again.", alert.TextContent.Trim());
        Assert.NotNull(menu.Find("[role=menu]"));

        menu.Find(".base-user-menu-sign-out").Click();

        SignOutService.Received(2).SignOutAsync();
    }

    [Fact]
    public void SignOut_Failed_ThenSucceeded_NavigatesAndClearsTheMessage()
    {
        SignOutService.SignOutAsync().Returns(SignOutResult.Failed, SignOutResult.SignedOut);
        var menu = RenderMenu();
        Open(menu);
        menu.Find(".base-user-menu-sign-out").Click();

        menu.Find(".base-user-menu-sign-out").Click();

        Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Empty(menu.FindAll(".base-user-menu-error"));
    }

    [Fact]
    public void SignOut_Failed_TheMessageDisappearsWhenTheMenuIsClosed()
    {
        SignOutService.SignOutAsync().Returns(SignOutResult.Failed);
        var menu = RenderMenu();
        Open(menu);
        menu.Find(".base-user-menu-sign-out").Click();

        Open(menu);
        Open(menu);

        Assert.Empty(menu.FindAll(".base-user-menu-error"));
    }

    [Fact]
    public void SignOut_NavigatesToTheConfiguredLoginPath()
    {
        SignOutService.SignOutAsync().Returns(SignOutResult.SignedOut);
        var menu = RenderMenu(parameters => parameters.Add(p => p.LoginPath, "account/sign-in"));
        Open(menu);

        menu.Find(".base-user-menu-sign-out").Click();

        Assert.EndsWith("/account/sign-in", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void SignOut_IsTheLastEntryOfTheMenu()
    {
        var menu = RenderMenu();
        Open(menu);

        var last = menu.FindAll("[role=menu] [role^=menuitem]").Last();
        Assert.Contains("base-user-menu-sign-out", last.ClassList);
        Assert.Equal("Sign out", last.TextContent.Trim());
    }

    [Fact]
    public void ShowLanguageFalse_HidesTheLanguageChoice()
    {
        var menu = RenderMenu(parameters => parameters.Add(p => p.ShowLanguage, false));
        Open(menu);

        Assert.Empty(menu.FindAll(".base-user-menu-languages"));
        Assert.NotEmpty(menu.FindAll(".base-user-menu-themes"));
    }

    [Fact]
    public void ASingleLanguage_HidesTheLanguageChoice()
    {
        var menu = RenderMenu(parameters => parameters.Add(p => p.Languages, ["en"]));
        Open(menu);

        Assert.Empty(menu.FindAll(".base-user-menu-languages"));
    }

    [Fact]
    public void ShowThemeFalse_HidesTheThemeChoice()
    {
        var menu = RenderMenu(parameters => parameters.Add(p => p.ShowTheme, false));
        Open(menu);

        Assert.Empty(menu.FindAll(".base-user-menu-themes"));
        Assert.NotEmpty(menu.FindAll(".base-user-menu-languages"));
    }

    [Fact]
    public void LanguageAndThemeHidden_LeavesOnlySignOutWithoutSeparator()
    {
        var menu = RenderMenu(parameters => parameters.Add(p => p.ShowLanguage, false).Add(p => p.ShowTheme, false));
        Open(menu);

        Assert.Single(menu.FindAll("[role=menu] [role^=menuitem]"));
        Assert.Empty(menu.FindAll("[role=separator]"));
    }

    [Fact]
    public void ExtraItems_AreListedAboveSignOut_AsLinksOrButtons()
    {
        var clicked = false;
        var menu = RenderMenu(parameters => parameters.Add(p => p.Items,
        [
            new UserMenuItem { Key = "settings", Label = "Settings", Href = "settings", Icon = new Size20.Settings() },
            new UserMenuItem { Key = "profile", Label = "Profile", OnClick = () => { clicked = true; return Task.CompletedTask; } },
        ]));
        Open(menu);

        var link = menu.Find("a[data-item-key=settings]");
        Assert.Equal("menuitem", link.GetAttribute("role"));
        Assert.Equal("settings", link.GetAttribute("href"));
        Assert.Equal("Settings", link.TextContent.Trim());

        var entries = menu.FindAll("[role=menu] [role^=menuitem]").Select(entry => entry.TextContent.Trim()).ToList();
        Assert.Equal(["Settings", "Profile", "Sign out"], entries.TakeLast(3));

        menu.Find("button[data-item-key=profile]").Click();

        Assert.True(clicked);
        Assert.Empty(menu.FindAll("[role=menu]"));
    }

    [Fact]
    public void ChoosingALinkItem_ClosesTheMenu()
    {
        var menu = RenderMenu(parameters => parameters.Add(p => p.Items,
            [new UserMenuItem { Key = "settings", Label = "Settings", Href = "settings" }]));
        Open(menu);

        menu.Find("a[data-item-key=settings]").Click();

        Assert.Empty(menu.FindAll("[role=menu]"));
    }

    [Fact]
    public void WithoutADisplayName_TheInitialsComeFromTheAccount()
    {
        Authorization.SetAuthorized("grace@example.com");
        Authorization.SetClaims(new Claim(ClaimTypes.Email, "grace@example.com"));

        var menu = RenderMenu();
        Open(menu);

        Assert.Equal("G", menu.Find(".base-user-menu-avatar").TextContent);
        Assert.Empty(menu.FindAll(".base-user-menu-account"));
    }

    [Fact]
    public void AvatarSrc_ShowsThePictureInsteadOfTheInitials()
    {
        var menu = RenderMenu(parameters => parameters.Add(p => p.AvatarSrc, "avatars/ada.png"));

        var image = menu.Find("button.base-user-menu-toggle img");
        Assert.Equal("avatars/ada.png", image.GetAttribute("src"));
        Assert.Equal(string.Empty, image.GetAttribute("alt"));
        Assert.Empty(menu.FindAll("span.base-user-menu-avatar"));
    }

    [Fact]
    public void SigningOutElsewhere_RemovesTheMenu()
    {
        var menu = RenderMenu();

        Authorization.SetNotAuthorized();

        menu.WaitForAssertion(() => Assert.Empty(menu.FindAll(".base-user-menu")));
    }

    [Fact]
    public void German_UsesTheGermanTexts()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");

        var menu = RenderMenu();
        Open(menu);

        Assert.Equal("Kontomenü von Ada Lovelace", ToggleOf(menu).GetAttribute("aria-label"));
        Assert.Equal("Abmelden", menu.Find(".base-user-menu-sign-out").TextContent.Trim());
        SignOutService.SignOutAsync().Returns(SignOutResult.Failed);
        menu.Find(".base-user-menu-sign-out").Click();
        Assert.Equal("Abmelden hat nicht geklappt – bitte erneut versuchen.", menu.Find(".base-user-menu-error").TextContent.Trim());
        Assert.Equal(["System", "Hell", "Dunkel"], menu.FindAll(".base-user-menu-themes [role=menuitemradio]").Select(entry => entry.TextContent.Trim()));
    }
}
