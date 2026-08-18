using BlazorBase.User.Models;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Server.Test.Controllers;

/// <summary>
/// Tests for the self-service profile endpoints in
/// <see cref="BlazorBase.User.Server.Controllers.UserControllerBase{TUser}"/>: reading the current
/// profile from the JWT subject claim and persisting theme/language preferences.
/// </summary>
public sealed class UserControllerTests : IdentityServerTestBase
{
    [Fact]
    public async Task GetMe_ReturnsProfile_ForAuthenticatedUser()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada Lovelace", role: "Admin");

        using var scope = CreateScope();
        var controller = CreateUserController(scope, PrincipalFor(user.Id));

        var result = await controller.GetMe();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfile>(ok.Value);
        Assert.Equal(user.Id, profile.Id);
        Assert.Equal("ada@example.com", profile.Email);
        Assert.Equal("Ada Lovelace", profile.DisplayName);
        Assert.Equal("Admin", profile.Role);
        Assert.Equal("System", profile.ThemePreference);
        Assert.Equal("en", profile.Language);
    }

    [Fact]
    public async Task GetMe_ReturnsNotFound_WhenNoSubjectClaim()
    {
        using var scope = CreateScope();
        var controller = CreateUserController(scope);

        var result = await controller.GetMe();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetMe_ReturnsNotFound_WhenUserNoLongerExists()
    {
        using var scope = CreateScope();
        var controller = CreateUserController(scope, PrincipalFor("missing-user-id"));

        var result = await controller.GetMe();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateSettings_PersistsThemeAndLanguage()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");

        using (var scope = CreateScope())
        {
            var controller = CreateUserController(scope, PrincipalFor(user.Id));
            var result = await controller.UpdateSettings(new UpdateUserSettingsRequest
            {
                ThemePreference = "Dark",
                Language = "de",
            });

            Assert.IsType<NoContentResult>(result);
        }

        using var verifyScope = CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var reloaded = await userManager.FindByIdAsync(user.Id);

        Assert.Equal("Dark", reloaded!.ThemePreference);
        Assert.Equal("de", reloaded.Language);
    }
}
