using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// The language chosen in the user menu is stored on the account, because the next start resolves the language
/// from the account's claim. It stays out of the way otherwise: no request for a visitor, none when the account
/// already holds the language, and no failure surfaced to an offline user.
/// </summary>
public sealed class UserLanguagePreferenceSyncTests
{
    private sealed class SignedInAuthStateProvider(ITokenStorage tokenStorage, IAuthService authService)
        : BlazorBaseUserAuthStateProvider(tokenStorage, authService, new CsrfTokenProvider())
    {
        public List<LoginResponse> AuthenticatedWith { get; } = [];

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "user")], "test"))));

        public override Task MarkUserAsAuthenticated(LoginResponse tokens)
        {
            AuthenticatedWith.Add(tokens);
            return Task.CompletedTask;
        }
    }

    private readonly IUserService UserService = Substitute.For<IUserService>();
    private readonly IAuthService AuthService = Substitute.For<IAuthService>();
    private readonly ITokenStorage TokenStorage = Substitute.For<ITokenStorage>();

    [Fact]
    public async Task Save_SendsTheLanguage_AndKeepsTheThemeOfTheProfile()
    {
        UserService.GetMeAsync().Returns(new UserProfile { ThemePreference = "Dark", Language = "en" });

        await CreateSync(authenticated: true).SaveAsync("de");

        await UserService.Received(1).UpdateSettingsAsync(Arg.Is<UpdateUserSettingsRequest>(request =>
            request.ThemePreference == "Dark" && request.Language == "de"));
    }

    [Fact]
    public async Task Save_DoesNothing_ForAnAnonymousVisitor()
    {
        await CreateSync(authenticated: false).SaveAsync("de");

        await UserService.DidNotReceive().GetMeAsync();
        await UserService.DidNotReceive().UpdateSettingsAsync(Arg.Any<UpdateUserSettingsRequest>());
    }

    [Fact]
    public async Task Save_DoesNothing_WhenTheAccountAlreadyHoldsTheLanguage()
    {
        UserService.GetMeAsync().Returns(new UserProfile { ThemePreference = "System", Language = "de" });

        await CreateSync(authenticated: true).SaveAsync("de");

        await UserService.DidNotReceive().UpdateSettingsAsync(Arg.Any<UpdateUserSettingsRequest>());
    }

    [Fact]
    public async Task Save_DoesNotThrow_WhenTheServerCannotBeReached()
    {
        UserService.GetMeAsync().ThrowsAsync(new HttpRequestException("offline"));

        await CreateSync(authenticated: true).SaveAsync("de");

        await UserService.DidNotReceive().UpdateSettingsAsync(Arg.Any<UpdateUserSettingsRequest>());
    }

    [Fact]
    public async Task Save_Bearer_RefreshesTheTokenPairSoTheNewClaimIsRead()
    {
        UserService.GetMeAsync().Returns(new UserProfile { ThemePreference = "System", Language = "en" });
        TokenStorage.GetTokensAsync().Returns(new LoginResponse { AccessToken = "access-1", RefreshToken = "refresh-1" });
        var refreshed = new LoginResponse { AccessToken = "access-2", RefreshToken = "refresh-2" };
        AuthService.RefreshAsync("refresh-1").Returns(refreshed);
        var authStateProvider = new SignedInAuthStateProvider(TokenStorage, AuthService);
        var services = new ServiceCollection()
            .AddSingleton(UserService)
            .AddSingleton(AuthService)
            .AddSingleton(TokenStorage)
            .BuildServiceProvider();

        await new UserLanguagePreferenceSync(services, authStateProvider).SaveAsync("de");

        Assert.Equal([refreshed], authStateProvider.AuthenticatedWith);
    }

    [Fact]
    public async Task Save_Cookie_DoesNotRefreshTokens()
    {
        UserService.GetMeAsync().Returns(new UserProfile { ThemePreference = "System", Language = "en" });
        TokenStorage.GetTokensAsync().Returns((LoginResponse?)null);
        var services = new ServiceCollection()
            .AddSingleton(UserService)
            .AddSingleton(AuthService)
            .AddSingleton(TokenStorage)
            .BuildServiceProvider();

        await new UserLanguagePreferenceSync(services, CreateAuthenticationProvider(authenticated: true)).SaveAsync("de");

        await UserService.Received(1).UpdateSettingsAsync(Arg.Any<UpdateUserSettingsRequest>());
        await AuthService.DidNotReceive().RefreshAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Save_DoesNothing_WhenNoUserServiceIsRegistered()
    {
        var sync = new UserLanguagePreferenceSync(new ServiceCollection().BuildServiceProvider(), CreateAuthenticationProvider(authenticated: true));

        await sync.SaveAsync("de");

        await UserService.DidNotReceive().GetMeAsync();
    }

    private UserLanguagePreferenceSync CreateSync(bool authenticated)
    {
        var services = new ServiceCollection().AddSingleton(UserService).BuildServiceProvider();
        return new UserLanguagePreferenceSync(services, CreateAuthenticationProvider(authenticated));
    }

    private static AuthenticationStateProvider CreateAuthenticationProvider(bool authenticated)
    {
        var identity = authenticated ? new ClaimsIdentity([new Claim(ClaimTypes.Name, "user")], "test") : new ClaimsIdentity();
        var provider = Substitute.For<AuthenticationStateProvider>();
        provider.GetAuthenticationStateAsync().Returns(new AuthenticationState(new ClaimsPrincipal(identity)));
        return provider;
    }
}
