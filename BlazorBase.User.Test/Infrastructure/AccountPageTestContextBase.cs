using BlazorBase.Components.Services;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace BlazorBase.User.Test.Infrastructure;

/// <summary>
/// Base bUnit context for the anonymous account pages: localization, a substituted <see cref="IAuthService"/>
/// and <see cref="ITokenStorage"/> behind a real <see cref="BlazorBaseUserAuthStateProvider"/>, and an
/// <see cref="ILanguageService"/> that reports English.
/// </summary>
public abstract class AccountPageTestContextBase : EnglishUserBunitTestContextBase
{
    protected IAuthService AuthService { get; } = Substitute.For<IAuthService>();

    protected ITokenStorage TokenStorage { get; } = Substitute.For<ITokenStorage>();

    protected AccountPageTestContextBase()
    {
        Services.AddLocalization();

        AuthService.GetSessionAsync(Arg.Any<string?>()).Returns(new AuthSessionResponse());
        TokenStorage.GetTokensAsync().Returns(Task.FromResult<LoginResponse?>(null));

        Services.AddSingleton(AuthService);
        Services.AddSingleton(TokenStorage);
        Services.AddSingleton<CsrfTokenProvider>();
        Services.AddSingleton<BlazorBaseUserAuthStateProvider>();

        var languageService = Substitute.For<ILanguageService>();
        languageService.CurrentLanguage.Returns("en");
        Services.AddSingleton(languageService);
    }
}
