using System.Reflection;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.Controllers;

/// <summary>
/// End-to-end tests for the auth endpoints in <see cref="BlazorBase.User.Server.Controllers.AuthControllerBase{TUser}"/>
/// against a real Identity stack over in-memory SQLite, focusing on the first-user setup, the login
/// lockout policy, and the security-critical refresh-token rotation flow.
/// </summary>
public sealed class AuthControllerTests : IdentityServerTestBase
{
    [Fact]
    public void Logout_IsAnonymous_SoItRevokesEvenWhenAccessTokenExpired()
    {
        var logout = typeof(BlazorBase.User.Server.Controllers.AuthControllerBase<TestUser>)
            .GetMethod("Logout", BindingFlags.Public | BindingFlags.Instance)!;

        Assert.NotNull(logout.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
        Assert.Null(logout.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
    }

    [Fact]
    public async Task Setup_CreatesFirstAdmin_WhenNoUsersExist()
    {
        var result = await SetupAsync(new SetupRequest
        {
            Email = "admin@example.com",
            DisplayName = "Admin",
            Password = "Secret123",
        });

        var login = AssertOkLogin(result);
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.False(string.IsNullOrEmpty(login.RefreshToken));

        using var verifyScope = CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByEmailAsync("admin@example.com");
        Assert.NotNull(user);
        Assert.Contains("Admin", await userManager.GetRolesAsync(user!));
    }

    [Fact]
    public async Task Setup_ReturnsConflict_WhenUsersAlreadyExist()
    {
        await SeedUserAsync("existing@example.com", "Existing");

        var result = await SetupAsync(new SetupRequest
        {
            Email = "second@example.com",
            DisplayName = "Second",
            Password = "Secret123",
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_ReturnsDistinctAccessAndRefreshTokens_OnValidCredentials()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));

        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.False(string.IsNullOrEmpty(login.RefreshToken));
        Assert.NotEqual(login.AccessToken, login.RefreshToken);
    }

    [Fact]
    public async Task Login_IncludesSecurityStampClaim_InIssuedAccessToken()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));

        var principal = UnprotectAccessToken(login.AccessToken);
        var securityStampClaim = principal.FindFirst("AspNet.Identity.SecurityStamp");

        Assert.NotNull(securityStampClaim);
        Assert.Equal(user.SecurityStamp, securityStampClaim!.Value);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_OnUnknownEmail()
    {
        var result = await LoginAsync("nobody@example.com", "Secret123");

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_OnWrongPassword()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var result = await LoginAsync("ada@example.com", "WrongPass1");

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_IncrementsAccessFailedCount_OnWrongPassword()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");

        await LoginAsync("ada@example.com", "WrongPass1");

        Assert.Equal(1, await GetAccessFailedCountAsync(user.Id));
    }

    [Fact]
    public async Task Login_ResetsAccessFailedCount_OnSuccess()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");

        await LoginAsync("ada@example.com", "WrongPass1");
        Assert.Equal(1, await GetAccessFailedCountAsync(user.Id));

        await LoginAsync("ada@example.com", "Secret123");
        Assert.Equal(0, await GetAccessFailedCountAsync(user.Id));
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenAccountInactive()
    {
        await SeedUserAsync("ada@example.com", "Ada", isActive: false);

        var result = await LoginAsync("ada@example.com", "Secret123");

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenLockedOut()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        await LockOutAsync(user.Id);

        var result = await LoginAsync("ada@example.com", "Secret123");

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_ReturnsIdenticalGenericMessage_ForUnknownEmailWrongPasswordAndLockout()
    {
        await SeedUserAsync("ada@example.com", "Ada");
        var lockedOutUser = await SeedUserAsync("locked@example.com", "Locked");
        await LockOutAsync(lockedOutUser.Id);

        var unknownEmailMessage = GetUnauthorizedMessage(await LoginAsync("nobody@example.com", "Secret123"));
        var wrongPasswordMessage = GetUnauthorizedMessage(await LoginAsync("ada@example.com", "WrongPass1"));
        var lockedOutMessage = GetUnauthorizedMessage(await LoginAsync("locked@example.com", "Secret123"));

        Assert.Equal("Invalid email or password.", unknownEmailMessage);
        Assert.Equal(unknownEmailMessage, wrongPasswordMessage);
        Assert.Equal(unknownEmailMessage, lockedOutMessage);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenUserLockedOut()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        await LockOutAsync(user.Id);

        var result = await RefreshAsync(login.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Refresh_IssuesADifferentRefreshToken_ThanThePresentedOne()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        var refresh = AssertOkLogin(await RefreshAsync(login.RefreshToken));

        Assert.NotEqual(login.RefreshToken, refresh.RefreshToken);
        Assert.NotEqual(login.AccessToken, refresh.AccessToken);
    }

    /// <summary>
    /// A refresh token must not be replayable once it has already been used: the token is revoked in
    /// the same transaction that issues its replacement, so presenting it again finds an already-
    /// revoked row.
    /// </summary>
    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenTheSameRefreshTokenIsPresentedTwice()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        AssertOkLogin(await RefreshAsync(login.RefreshToken));

        var replay = await RefreshAsync(login.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(replay.Result);
    }

    /// <summary>
    /// The regression this fix closes: rotating the security stamp on every refresh (to fake
    /// single-use without a revocation table) signed every other session for the account out too.
    /// Two independent logins for the same user represent two independent devices; refreshing one
    /// must not disturb the other.
    /// </summary>
    [Fact]
    public async Task Refresh_OfOneSession_LeavesAnotherSessionsRefreshTokenUsable()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var sessionA = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        var sessionB = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));

        AssertOkLogin(await RefreshAsync(sessionA.RefreshToken));

        var resultB = await RefreshAsync(sessionB.RefreshToken);

        AssertOkLogin(resultB);
    }

    /// <summary>
    /// Same root cause as <see cref="Refresh_OfOneSession_LeavesAnotherSessionsRefreshTokenUsable"/>,
    /// proven directly on the shared signal a cookie session and a bearer refresh both depend on: a
    /// bearer refresh must not rotate the account's security stamp, or every cookie session for the
    /// same user is signed out the moment a bearer client refreshes.
    /// </summary>
    [Fact]
    public async Task Refresh_DoesNotRotateTheUsersSecurityStamp()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        var stampBeforeRefresh = user.SecurityStamp;

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        AssertOkLogin(await RefreshAsync(login.RefreshToken));

        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var reloaded = await userManager.FindByIdAsync(user.Id);

        Assert.Equal(stampBeforeRefresh, reloaded!.SecurityStamp);
    }

    /// <summary>
    /// Replaying an already-rotated token is the theft signal the rotation scheme exists to catch:
    /// it must revoke the whole family, taking the legitimately rotated sibling down with it too.
    /// </summary>
    [Fact]
    public async Task Refresh_ReplayOfAnAlreadyRotatedToken_RevokesTheWholeFamily()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        var rotated = AssertOkLogin(await RefreshAsync(login.RefreshToken));

        await RefreshAsync(login.RefreshToken);

        var result = await RefreshAsync(rotated.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Refresh_MultiStepRotationChain_ContinuesToWork_WhenNoReplayOccurs()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        var secondToken = AssertOkLogin(await RefreshAsync(login.RefreshToken));
        var thirdToken = AssertOkLogin(await RefreshAsync(secondToken.RefreshToken));

        Assert.NotEqual(login.RefreshToken, secondToken.RefreshToken);
        Assert.NotEqual(secondToken.RefreshToken, thirdToken.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_OnExpiredToken()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        TimeProvider.Advance(TimeSpan.FromDays(31));

        var result = await RefreshAsync(login.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_OnGarbageToken()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var result = await RefreshAsync("not-a-real-token");

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenUserDeactivated()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        await SetUserActiveAsync(user.Id, false);

        var result = await RefreshAsync(login.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    /// <summary>
    /// Rotating a user's security stamp - by any means, not only through this controller - must
    /// invalidate an outstanding refresh token, even though its database row is untouched by that
    /// rotation: the stamp captured at issuance is re-checked against the live user on every refresh.
    /// </summary>
    [Fact]
    public async Task Refresh_ReturnsUnauthorized_AfterTheUsersSecurityStampIsRotatedDirectly()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));

        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
            var reloaded = await userManager.FindByIdAsync(user.Id);
            await userManager.UpdateSecurityStampAsync(reloaded!);
        }

        var result = await RefreshAsync(login.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Logout_ReturnsNoContent_ForUnknownToken()
    {
        var result = await LogoutAsync("unknown-token");

        Assert.IsType<NoContentResult>(result);
    }

    /// <summary>
    /// Logout must make the previously issued refresh token unusable, not merely clear the transport.
    /// Enforced by revoking the token's family - not by rotating the security stamp, which would also
    /// sign out every other session for the account.
    /// </summary>
    [Fact]
    public async Task Logout_ThenRefresh_ReturnsUnauthorized()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        await LogoutAsync(login.RefreshToken);

        var result = await RefreshAsync(login.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    /// <summary>
    /// Logging out of one device must not sign the user out everywhere: only the presented token's
    /// family is revoked, so a second session for the same user keeps working.
    /// </summary>
    [Fact]
    public async Task Logout_OfOneSession_LeavesAnotherSessionsRefreshTokenUsable()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var sessionA = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        var sessionB = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));

        await LogoutAsync(sessionA.RefreshToken);

        var resultB = await RefreshAsync(sessionB.RefreshToken);

        AssertOkLogin(resultB);
    }

    /// <summary>
    /// A password reset must still kill every outstanding session, including a refresh token minted
    /// before it - the one case where invalidating every session for the account is correct.
    /// </summary>
    [Fact]
    public async Task PasswordReset_InvalidatesAnOutstandingRefreshToken()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));

        using (var scope = CreateScope())
        {
            var dataProvider = new UserDataProvider<TestUser>(
                scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>(),
                scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
                scope.ServiceProvider.GetRequiredService<TestUserDbContext>());

            await dataProvider.PatchAsync(user.Id, new Dictionary<string, object?> { ["Password"] = "NewSecret123" });
        }

        var result = await RefreshAsync(login.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    private async Task<ActionResult<LoginResponse>> SetupAsync(SetupRequest request)
    {
        using var scope = CreateScope();
        return await CreateAuthController(scope).Setup(request);
    }

    private async Task<ActionResult<LoginResponse>> LoginAsync(string email, string password)
    {
        using var scope = CreateScope();
        return await CreateAuthController(scope).Login(new LoginRequest { Email = email, Password = password });
    }

    private async Task<ActionResult<LoginResponse>> RefreshAsync(string refreshToken)
    {
        using var scope = CreateScope();
        return await CreateAuthController(scope).Refresh(new RefreshTokenRequest { RefreshToken = refreshToken });
    }

    private async Task<IActionResult> LogoutAsync(string refreshToken)
    {
        using var scope = CreateScope();
        return await CreateAuthController(scope).Logout(new RefreshTokenRequest { RefreshToken = refreshToken });
    }

    private static LoginResponse AssertOkLogin(ActionResult<LoginResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<LoginResponse>(ok.Value);
    }

    private static string GetUnauthorizedMessage(ActionResult<LoginResponse> result)
    {
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var messageProperty = unauthorized.Value!.GetType().GetProperty("message")!;
        return (string)messageProperty.GetValue(unauthorized.Value)!;
    }

    private async Task SetUserActiveAsync(string userId, bool isActive)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByIdAsync(userId);
        user!.IsActive = isActive;
        await userManager.UpdateAsync(user);
    }

    private async Task LockOutAsync(string userId)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByIdAsync(userId);
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddYears(1));
    }

    private async Task<int> GetAccessFailedCountAsync(string userId)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByIdAsync(userId);
        return await userManager.GetAccessFailedCountAsync(user!);
    }
}
