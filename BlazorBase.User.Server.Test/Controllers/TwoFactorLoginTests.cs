using BlazorBase.User.Models;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.Controllers;

/// <summary>
/// Covers the two-factor login branch of <see cref="BlazorBase.User.Server.Controllers.AuthControllerBase{TUser}"/>:
/// a correct password against a 2FA-enabled account answers <c>202</c> with only a pending token, that
/// token completes the login with a correct authenticator or recovery code, and the pending token is
/// provably unusable as an access token, a refresh token, or after it expires.
/// </summary>
public sealed class TwoFactorLoginTests : IdentityServerTestBase
{
    [Fact]
    public async Task Login_ReturnsAccepted_WithOnlyAPendingToken_WhenTwoFactorIsEnabled()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        await EnableAuthenticatorTwoFactorAsync(user.Id);

        var result = await LoginAsync("ada@example.com", "Secret123456");

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        var body = Assert.IsType<TwoFactorRequiredResponse>(accepted.Value);
        Assert.False(string.IsNullOrEmpty(body.PendingToken));
    }

    [Fact]
    public async Task LoginTwoFactor_IssuesFullTokenPair_WithACorrectCode()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        var key = await EnableAuthenticatorTwoFactorAsync(user.Id);

        var pendingToken = await GetPendingTokenAsync("ada@example.com", "Secret123456");
        var code = TotpTestHelper.GenerateCode(key, DateTimeOffset.UtcNow);

        var result = await LoginTwoFactorAsync(pendingToken, code);

        var login = AssertOkLogin(result);
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.False(string.IsNullOrEmpty(login.RefreshToken));
    }

    [Fact]
    public async Task LoginTwoFactor_ReturnsUnauthorized_OnWrongCode()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        await EnableAuthenticatorTwoFactorAsync(user.Id);

        var pendingToken = await GetPendingTokenAsync("ada@example.com", "Secret123456");

        var result = await LoginTwoFactorAsync(pendingToken, "000000");

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task LoginTwoFactor_TripsLockout_OnRepeatedWrongCodes()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        await EnableAuthenticatorTwoFactorAsync(user.Id);

        var pendingToken = await GetPendingTokenAsync("ada@example.com", "Secret123456");

        for (var attempt = 0; attempt < 5; attempt++)
            await LoginTwoFactorAsync(pendingToken, "000000");

        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        Assert.True(await userManager.IsLockedOutAsync((await userManager.FindByIdAsync(user.Id))!));
    }

    [Fact]
    public async Task LoginTwoFactor_ReturnsUnauthorized_AfterThePendingTokenExpires()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        var key = await EnableAuthenticatorTwoFactorAsync(user.Id);

        var pendingToken = await GetPendingTokenAsync("ada@example.com", "Secret123456");
        var code = TotpTestHelper.GenerateCode(key, DateTimeOffset.UtcNow);

        TimeProvider.Advance(TimeSpan.FromMinutes(6));

        var result = await LoginTwoFactorAsync(pendingToken, code);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task LoginTwoFactor_RecoveryCode_WorksOnceAndNotTwice()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        await EnableAuthenticatorTwoFactorAsync(user.Id);

        string recoveryCode;
        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
            var reloaded = await userManager.FindByIdAsync(user.Id);
            var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(reloaded!, 5);
            recoveryCode = codes!.First();
        }

        var firstPendingToken = await GetPendingTokenAsync("ada@example.com", "Secret123456");
        AssertOkLogin(await LoginTwoFactorAsync(firstPendingToken, recoveryCode, isRecoveryCode: true));

        var secondPendingToken = await GetPendingTokenAsync("ada@example.com", "Secret123456");
        var secondAttempt = await LoginTwoFactorAsync(secondPendingToken, recoveryCode, isRecoveryCode: true);

        Assert.IsType<UnauthorizedObjectResult>(secondAttempt.Result);
    }

    /// <summary>
    /// The proof that the pending-token protector purpose is genuinely distinct from the access-token
    /// protector's: unprotecting a pending token through the bearer scheme's own protector must fail,
    /// not merely produce a principal this endpoint happens to reject.
    /// </summary>
    [Fact]
    public async Task PendingToken_DoesNotUnprotectAsAnAccessToken()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        await EnableAuthenticatorTwoFactorAsync(user.Id);

        var pendingToken = await GetPendingTokenAsync("ada@example.com", "Secret123456");

        using var scope = CreateScope();
        var bearerTokenOptions = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(IdentityConstants.BearerScheme);

        Assert.Null(bearerTokenOptions.BearerTokenProtector.Unprotect(pendingToken));
    }

    /// <summary>
    /// The mirror proof: a real access token must not unprotect as a pending token either.
    /// </summary>
    [Fact]
    public async Task AccessToken_DoesNotUnprotectAsAPendingToken()
    {
        await SeedUserAsync("ada@example.com", "Ada");
        var login = AssertOkLogin(await LoginAsync("ada@example.com", "Secret123456"));

        using var scope = CreateScope();
        var pendingTokenService = scope.ServiceProvider.GetRequiredService<TwoFactorPendingTokenService<TestUser>>();

        Assert.Null(pendingTokenService.Unprotect(login.AccessToken));
    }

    /// <summary>
    /// A pending token is not a row in the refresh-token table, so presenting it to <c>refresh</c> can
    /// only ever be an unknown token - the same outcome as any other garbage input.
    /// </summary>
    [Fact]
    public async Task PendingToken_CannotBeUsedAsARefreshToken()
    {
        var user = await SeedUserAsync("ada@example.com", "Ada");
        await EnableAuthenticatorTwoFactorAsync(user.Id);

        var pendingToken = await GetPendingTokenAsync("ada@example.com", "Secret123456");

        using var scope = CreateScope();
        var result = await CreateAuthController(scope).Refresh(new RefreshTokenRequest { RefreshToken = pendingToken });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    private async Task<string> EnableAuthenticatorTwoFactorAsync(string userId)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByIdAsync(userId);

        await userManager.ResetAuthenticatorKeyAsync(user!);
        var key = (await userManager.GetAuthenticatorKeyAsync(user!))!;
        await userManager.SetTwoFactorEnabledAsync(user!, true);

        return key;
    }

    private async Task<string> GetPendingTokenAsync(string email, string password)
    {
        var accepted = Assert.IsType<AcceptedResult>((await LoginAsync(email, password)).Result);
        return Assert.IsType<TwoFactorRequiredResponse>(accepted.Value).PendingToken;
    }

    private async Task<ActionResult<LoginResponse>> LoginAsync(string email, string password)
    {
        using var scope = CreateScope();
        return await CreateAuthController(scope).Login(new LoginRequest { Email = email, Password = password });
    }

    private async Task<ActionResult<LoginResponse>> LoginTwoFactorAsync(string pendingToken, string code, bool isRecoveryCode = false)
    {
        using var scope = CreateScope();
        return await CreateAuthController(scope).LoginTwoFactor(new TwoFactorLoginRequest
        {
            PendingToken = pendingToken,
            Code = code,
            IsRecoveryCode = isRecoveryCode,
        });
    }

    private static LoginResponse AssertOkLogin(ActionResult<LoginResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<LoginResponse>(ok.Value);
    }
}
