using BlazorBase.User.Models;
using BlazorBase.User.Pages;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Pages;

/// <summary>
/// USER-02 proof: the credential fields on the combined login/first-run-setup page bind the FluentUI
/// <c>AutoComplete</c> parameter to an appropriate hint. Assertions read
/// <c>FluentTextField.Instance.AutoComplete</c> rather than the rendered markup, because FluentUI only
/// forwards that parameter to the underlying element's <c>autocomplete</c> HTML attribute imperatively
/// via JS interop in <c>OnAfterRenderAsync</c> (it never appears in the static/server-rendered render
/// tree bUnit inspects) — the same reason other FluentUI parameters in this project (e.g.
/// <c>TextFieldType</c> in <see cref="Components.UserCardRenderTests"/>) are asserted through the
/// component instance instead of the DOM.
/// </summary>
public class LoginAutocompleteTests : UserBunitTestContextBase
{
    [Fact]
    public void LoginMode_CredentialFields_CarryAutocompleteHints()
    {
        var cut = RenderLogin(hasUsers: true);

        var fields = cut.FindComponents<FluentTextField>();

        Assert.Equal(2, fields.Count);
        Assert.Equal("username", fields[0].Instance.AutoComplete);
        Assert.Equal("current-password", fields[1].Instance.AutoComplete);
    }

    [Fact]
    public void SetupMode_CredentialFields_CarryAutocompleteHints()
    {
        var cut = RenderLogin(hasUsers: false);

        var fields = cut.FindComponents<FluentTextField>();

        Assert.Equal(5, fields.Count);
        Assert.Equal("name", fields[0].Instance.AutoComplete);
        Assert.Equal("username", fields[1].Instance.AutoComplete);
        Assert.Equal("new-password", fields[2].Instance.AutoComplete);
        Assert.Equal("new-password", fields[3].Instance.AutoComplete);
        Assert.Equal("off", fields[4].Instance.AutoComplete);
    }

    private IRenderedComponent<Login> RenderLogin(bool hasUsers)
    {
        Services.AddLocalization();

        var authService = Substitute.For<IAuthService>();
        authService.GetStatusAsync().Returns(Task.FromResult(new AuthStatusResponse { HasUsers = hasUsers }));
        Services.AddSingleton(authService);

        var tokenStorage = Substitute.For<ITokenStorage>();
        tokenStorage.GetTokensAsync().Returns(Task.FromResult<LoginResponse?>(null));
        Services.AddSingleton(tokenStorage);
        Services.AddSingleton<BlazorBaseUserAuthStateProvider>();

        // Login consults this after a successful sign-in, to decide whether the destination has to
        // be reached through a full page load because the account is configured for another language.
        var languageService = Substitute.For<ILanguageService>();
        languageService.CurrentLanguage.Returns("en");
        Services.AddSingleton(languageService);

        Services.Configure<BlazorBaseUserClientOptions>(options => options.CollectHouseholdNameOnSetup = true);

        return Render<Login>(parameters => parameters
            .Add(p => p.CollectHouseholdName, true));
    }
}
