using BlazorBase.User.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Services;

/// <summary>
/// Stores the language choice on the signed-in user's account through <c>PUT api/user/me/settings</c>.
/// </summary>
/// <remarks>
/// The endpoint replaces theme and language together, so the current profile is read first and its theme is sent
/// back unchanged. Under the cookie transport the server reissues the session cookie with the new claim; under the
/// bearer transport the token pair is refreshed so the next session read carries it, as
/// <c>UserPreferencesPanel</c> does. Nothing is sent for an anonymous visitor, when no <see cref="IUserService"/>
/// is registered, or when the account already holds the language; a failing request is swallowed, so an offline
/// user still gets the switch for this session.
/// </remarks>
public sealed class UserLanguagePreferenceSync(IServiceProvider serviceProvider, AuthenticationStateProvider authenticationStateProvider) : IUserLanguagePreferenceSync
{
    #region Injects

    private readonly IServiceProvider ServiceProvider = serviceProvider;
    private readonly AuthenticationStateProvider AuthenticationStateProvider = authenticationStateProvider;

    #endregion

    public async Task SaveAsync(string language)
    {
        var userService = ServiceProvider.GetService<IUserService>();
        if (userService is null)
            return;

        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (authenticationState.User.Identity?.IsAuthenticated != true)
            return;

        try
        {
            var profile = await userService.GetMeAsync();
            if (String.Equals(profile.Language, language, StringComparison.OrdinalIgnoreCase))
                return;

            await userService.UpdateSettingsAsync(new UpdateUserSettingsRequest
            {
                ThemePreference = profile.ThemePreference,
                Language = language,
            });

            await RefreshBearerTokensAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
        }
    }

    private async Task RefreshBearerTokensAsync()
    {
        var tokenStorage = ServiceProvider.GetService<ITokenStorage>();
        var authService = ServiceProvider.GetService<IAuthService>();
        if (tokenStorage is null || authService is null)
            return;

        var tokens = await tokenStorage.GetTokensAsync();
        if (String.IsNullOrEmpty(tokens?.RefreshToken))
            return;

        var refreshedTokens = await authService.RefreshAsync(tokens.RefreshToken);

        if (AuthenticationStateProvider is BlazorBaseUserAuthStateProvider userAuthStateProvider)
        {
            await userAuthStateProvider.MarkUserAsAuthenticated(refreshedTokens);
            return;
        }

        await tokenStorage.SaveTokensAsync(refreshedTokens);
    }
}
