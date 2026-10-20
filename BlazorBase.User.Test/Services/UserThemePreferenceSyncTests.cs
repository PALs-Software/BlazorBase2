using System.Security.Claims;
using BlazorBase.Components.Services;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// The theme choice is saved on the account so it follows the user to another browser. It must stay
/// out of the way otherwise: no request for a visitor, none when the account already holds the value,
/// and no failure surfaced to a user whose connection is gone.
/// </summary>
public sealed class UserThemePreferenceSyncTests
{
    private readonly IUserService UserService = Substitute.For<IUserService>();

    [Fact]
    public async Task Save_SendsTheTheme_AndKeepsTheLanguageOfTheProfile()
    {
        UserService.GetMeAsync().Returns(new UserProfile { ThemePreference = "System", Language = "de" });

        await CreateSync(authenticated: true).SaveAsync(ThemePreference.Dark);

        await UserService.Received(1).UpdateSettingsAsync(Arg.Is<UpdateUserSettingsRequest>(request =>
            request.ThemePreference == "Dark" && request.Language == "de"));
    }

    [Fact]
    public async Task Save_DoesNothing_ForAnAnonymousVisitor()
    {
        await CreateSync(authenticated: false).SaveAsync(ThemePreference.Dark);

        await UserService.DidNotReceive().GetMeAsync();
        await UserService.DidNotReceive().UpdateSettingsAsync(Arg.Any<UpdateUserSettingsRequest>());
    }

    [Fact]
    public async Task Save_DoesNothing_WhenTheAccountAlreadyHoldsThePreference()
    {
        UserService.GetMeAsync().Returns(new UserProfile { ThemePreference = "Dark", Language = "en" });

        await CreateSync(authenticated: true).SaveAsync(ThemePreference.Dark);

        await UserService.DidNotReceive().UpdateSettingsAsync(Arg.Any<UpdateUserSettingsRequest>());
    }

    [Fact]
    public async Task Save_DoesNotThrow_WhenTheServerCannotBeReached()
    {
        UserService.GetMeAsync().ThrowsAsync(new HttpRequestException("offline"));

        await CreateSync(authenticated: true).SaveAsync(ThemePreference.Dark);

        await UserService.DidNotReceive().UpdateSettingsAsync(Arg.Any<UpdateUserSettingsRequest>());
    }

    [Fact]
    public async Task Save_DoesNotThrow_WhenTheUpdateIsRejected()
    {
        UserService.GetMeAsync().Returns(new UserProfile { ThemePreference = "System", Language = "en" });
        UserService.UpdateSettingsAsync(Arg.Any<UpdateUserSettingsRequest>()).ThrowsAsync(new HttpRequestException("500"));

        await CreateSync(authenticated: true).SaveAsync(ThemePreference.Light);
    }

    [Fact]
    public async Task Save_DoesNothing_WhenNoUserServiceIsRegistered()
    {
        var sync = new UserThemePreferenceSync(new ServiceCollection().BuildServiceProvider(), CreateAuthenticationProvider(authenticated: true));

        await sync.SaveAsync(ThemePreference.Dark);

        await UserService.DidNotReceive().GetMeAsync();
    }

    private UserThemePreferenceSync CreateSync(bool authenticated)
    {
        var services = new ServiceCollection().AddSingleton(UserService).BuildServiceProvider();
        return new UserThemePreferenceSync(services, CreateAuthenticationProvider(authenticated));
    }

    private static AuthenticationStateProvider CreateAuthenticationProvider(bool authenticated)
    {
        var identity = authenticated ? new ClaimsIdentity([new Claim(ClaimTypes.Name, "user")], "test") : new ClaimsIdentity();
        var provider = Substitute.For<AuthenticationStateProvider>();
        provider.GetAuthenticationStateAsync().Returns(new AuthenticationState(new ClaimsPrincipal(identity)));
        return provider;
    }
}
