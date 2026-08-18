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
public abstract class UserControllerBase<TUser>(UserManager<TUser> userManager) : ControllerBase
    where TUser : BaseUser
{
    #region Injects
    private readonly UserManager<TUser> UserManager = userManager;
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

        return NoContent();
    }

    private async Task<TUser?> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return null;

        return await UserManager.FindByIdAsync(userId);
    }
}
