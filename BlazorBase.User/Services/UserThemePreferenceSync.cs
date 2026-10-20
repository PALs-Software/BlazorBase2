using BlazorBase.Components.Services;
using BlazorBase.User.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Services;

/// <summary>
/// Stores the theme choice on the signed-in user's account through <c>PUT api/user/me/settings</c>.
/// </summary>
/// <remarks>
/// The endpoint replaces theme and language together, so the current profile is read first and its language is
/// sent back unchanged. Nothing is sent for an anonymous visitor, when no <see cref="IUserService"/> is
/// registered, or when the account already holds the preference; a failing request is swallowed because the
/// choice is already applied and saved in the browser, which is all an offline user can have.
/// </remarks>
public sealed class UserThemePreferenceSync(IServiceProvider serviceProvider, AuthenticationStateProvider authenticationStateProvider) : IThemePreferenceSync
{
    #region Injects

    private readonly IServiceProvider ServiceProvider = serviceProvider;
    private readonly AuthenticationStateProvider AuthenticationStateProvider = authenticationStateProvider;

    #endregion

    public async Task SaveAsync(ThemePreference preference)
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
            var theme = preference.ToString();

            if (profile.ThemePreference == theme)
                return;

            await userService.UpdateSettingsAsync(new UpdateUserSettingsRequest
            {
                ThemePreference = theme,
                Language = profile.Language,
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
        }
    }
}
