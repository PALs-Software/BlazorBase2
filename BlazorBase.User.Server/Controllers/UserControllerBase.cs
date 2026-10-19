using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Server.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public abstract class UserControllerBase<TUser>(UserManager<TUser> userManager, SignInManager<TUser> signInManager) : ControllerBase
    where TUser : BaseUser
{
    #region Injects
    private readonly UserManager<TUser> UserManager = userManager;
    private readonly SignInManager<TUser> SignInManager = signInManager;
    #endregion

    /// <summary>
    /// Returns the currently authenticated user's profile.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserProfile>> GetMe()
    {
        var user = await GetCurrentUser();
        if (user is null)
            return NotFound();

        var roles = await UserManager.GetRolesAsync(user);

        return Ok(new UserProfile
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            ThemePreference = user.ThemePreference,
            Language = user.Language,
            Role = roles.FirstOrDefault() ?? "User",
        });
    }

    /// <summary>
    /// Updates the current user's theme and language preferences.
    /// </summary>
    /// <remarks>
    /// Reissues the active cookie session via <see cref="SignInManager{TUser}.RefreshSignInAsync"/>
    /// so the new <c>language</c>/<c>themePreference</c> claims take effect immediately, without the
    /// caller having to sign out and back in. Under the bearer-token transport this is a no-op from
    /// the caller's point of view — no cookie is written, and the client already refreshes its own
    /// token pair after calling this endpoint (see <c>UserPreferencesPanel.razor.cs</c>).
    /// </remarks>
    [HttpPut("me/settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var user = await GetCurrentUser();
        if (user is null)
            return NotFound();

        user.ThemePreference = request.ThemePreference;
        user.Language = request.Language;

        var result = await UserManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await OnSettingsUpdatedAsync(user, request);
        await SignInManager.RefreshSignInAsync(user);

        return NoContent();
    }

    /// <summary>
    /// Hook called after the user's theme/language preferences have been persisted and before the
    /// active cookie session is refreshed. Override to keep a host-specific preference store (such
    /// as a separate domain user table) in sync with the same settings.
    /// </summary>
    protected virtual Task OnSettingsUpdatedAsync(TUser user, UpdateUserSettingsRequest request) => Task.CompletedTask;

    private async Task<TUser?> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return null;

        return await UserManager.FindByIdAsync(userId);
    }
}
