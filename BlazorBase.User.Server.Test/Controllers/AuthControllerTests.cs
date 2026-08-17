using System.Reflection;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.Controllers;

/// <summary>
/// End-to-end tests for the auth endpoints in <see cref="BlazorBase.User.Server.Controllers.AuthControllerBase{TUser}"/>
/// against a real Identity stack over in-memory SQLite, focusing on the first-user setup, the login
/// lockout policy, and the security-critical refresh-token rotation/revocation flow.
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
    public async Task Login_ReturnsTokens_OnValidCredentials()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        Assert.False(string.IsNullOrEmpty(login.AccessToken));

        var tokens = await ReadRefreshTokensAsync();
        Assert.Single(tokens);
        Assert.False(tokens[0].IsRevoked);
    }

    [Fact]
    public async Task Login_StoresHashedRefreshToken_NotRawValue()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));

        var tokens = await ReadRefreshTokensAsync();
        var storedToken = Assert.Single(tokens);

        Assert.Equal(HashToken(login.RefreshToken), storedToken.Token);
        Assert.NotEqual(login.RefreshToken, storedToken.Token);
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
    public async Task Refresh_RotatesRefreshToken_RevokingTheOldAndIssuingANew()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        var refresh = AssertOkLogin(await RefreshAsync(login.RefreshToken));

        Assert.NotEqual(login.RefreshToken, refresh.RefreshToken);

        var loginTokenHash = HashToken(login.RefreshToken);
        var refreshTokenHash = HashToken(refresh.RefreshToken);

        var tokens = await ReadRefreshTokensAsync();
        Assert.Equal(2, tokens.Count);
        Assert.True(tokens.Single(t => t.Token == loginTokenHash).IsRevoked);
        Assert.False(tokens.Single(t => t.Token == refreshTokenHash).IsRevoked);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenSameRawTokenIsReusedAfterRotation()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        AssertOkLogin(await RefreshAsync(login.RefreshToken));

        var reuse = await RefreshAsync(login.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(reuse.Result);
    }

    [Fact]
    public async Task Refresh_ReplayingARotatedToken_RevokesTheEntireFamily_SoTheRotatedTokenCanNoLongerRefresh()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        var afterFirstRotation = AssertOkLogin(await RefreshAsync(login.RefreshToken));

        var replay = await RefreshAsync(login.RefreshToken);
        Assert.IsType<UnauthorizedObjectResult>(replay.Result);

        var tokens = await ReadRefreshTokensAsync();
        var familyId = tokens.Single(t => t.Token == HashToken(login.RefreshToken)).FamilyId;

        Assert.True(tokens.All(t => t.FamilyId == familyId));
        Assert.True(tokens.All(t => t.IsRevoked));

        var reuseOfRotatedToken = await RefreshAsync(afterFirstRotation.RefreshToken);
        Assert.IsType<UnauthorizedObjectResult>(reuseOfRotatedToken.Result);
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

        var tokens = await ReadRefreshTokensAsync();
        Assert.Equal(3, tokens.Count);

        var thirdTokenHash = HashToken(thirdToken.RefreshToken);
        Assert.False(tokens.Single(t => t.Token == thirdTokenHash).IsRevoked);
        Assert.True(tokens.Where(t => t.Token != thirdTokenHash).All(t => t.IsRevoked));

        var familyIds = tokens.Select(t => t.FamilyId).Distinct().ToList();
        Assert.Single(familyIds);
    }

    [Fact]
    public async Task Refresh_LegacyEmptyFamilyToken_DoesNotSweepAnotherUsersEmptyFamilyTokens()
    {
        var userA = await SeedUserAsync("usera@example.com", "UserA");
        var userB = await SeedUserAsync("userb@example.com", "UserB");

        var userARevokedToken = await SeedRefreshTokenAsync(userA.Id, Guid.Empty, isRevoked: true);
        await SeedRefreshTokenAsync(userA.Id, Guid.Empty, isRevoked: false);
        await SeedRefreshTokenAsync(userB.Id, Guid.Empty, isRevoked: true);
        var userBValidToken = await SeedRefreshTokenAsync(userB.Id, Guid.Empty, isRevoked: false);

        var replay = await RefreshAsync(userARevokedToken);

        Assert.IsType<UnauthorizedObjectResult>(replay.Result);

        var tokens = await ReadRefreshTokensAsync();
        var userBValidTokenAfterReplay = tokens.Single(t => t.Token == HashToken(userBValidToken));
        Assert.False(userBValidTokenAfterReplay.IsRevoked);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_OnRevokedToken()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        await MutateTokensAsync(token => token.IsRevoked = true);

        var result = await RefreshAsync(login.RefreshToken);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_OnExpiredToken()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        await MutateTokensAsync(token => token.ExpiresAt = DateTime.UtcNow.AddDays(-1));

        var result = await RefreshAsync(login.RefreshToken);

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

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        var result = await LogoutAsync(login.RefreshToken);

        Assert.IsType<NoContentResult>(result);

        var tokens = await ReadRefreshTokensAsync();
        Assert.True(tokens.Single().IsRevoked);
    }

    [Fact]
    public async Task Logout_ReturnsNoContent_ForUnknownToken()
    {
        var result = await LogoutAsync("unknown-token");

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Logout_ThenRefresh_ReturnsUnauthorized()
    {
        await SeedUserAsync("ada@example.com", "Ada");

        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123"));
        await LogoutAsync(login.RefreshToken);

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

    private async Task<List<RefreshToken>> ReadRefreshTokensAsync()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        return await context.RefreshTokens.AsNoTracking().ToListAsync();
    }

    private string HashToken(string rawToken)
    {
        using var scope = CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<TokenService<TestUser>>();
        return tokenService.HashRefreshToken(rawToken);
    }

    private async Task<string> SeedRefreshTokenAsync(string userId, Guid familyId, bool isRevoked)
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<TokenService<TestUser>>();

        var rawToken = tokenService.GenerateRefreshToken();
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = tokenService.HashRefreshToken(rawToken),
            UserId = userId,
            FamilyId = familyId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = isRevoked,
        });
        await context.SaveChangesAsync();

        return rawToken;
    }

    private async Task MutateTokensAsync(Action<RefreshToken> mutate)
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        var tokens = await context.RefreshTokens.ToListAsync();
        foreach (var token in tokens)
            mutate(token);
        await context.SaveChangesAsync();
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
        await userManager.SetLockoutEnabledAsync(user!, true);
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(10));
    }

    private async Task<int> GetAccessFailedCountAsync(string userId)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByIdAsync(userId);
        return await userManager.GetAccessFailedCountAsync(user!);
    }
}
