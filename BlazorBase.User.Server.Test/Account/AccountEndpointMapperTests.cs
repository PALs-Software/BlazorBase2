using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Account;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Security;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.Account;

/// <summary>
/// End-to-end coverage for <c>MapBlazorBaseAccountEndpoints</c> through a real <see cref="TestServer"/>:
/// <c>forgot-password</c> answers identically for a known and an unknown address, a password-reset
/// token cannot be redeemed twice, and every <c>2fa</c> management endpoint rejects an anonymous
/// caller. No test here sends real email - <see cref="FakeEmailSender{TUser}"/> is substituted.
/// </summary>
public sealed class AccountEndpointMapperTests
{
    private static Task<IHost> StartHostAsync() => StartHostAsync(timingFloorMinimumMilliseconds: 0, timingFloorMaximumMilliseconds: 0);

    /// <summary>
    /// The timing floor defaults to <c>0</c> (disabled) so the suite stays fast; tests that exercise
    /// the floor itself pass a small non-zero value instead of the production default, which would
    /// make every one of them take at least three quarters of a second. <paramref name="emailSenderThrows"/>
    /// swaps the stub sender for one that always throws, so a test can prove the account endpoints
    /// stay indistinguishable from the unknown-address case even when the send behind them fails.
    /// </summary>
    private static async Task<IHost> StartHostAsync(
        int timingFloorMinimumMilliseconds,
        int timingFloorMaximumMilliseconds,
        bool emailSenderThrows = false)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthTokens:AccessTokenMinutes"] = "15",
                ["AuthTokens:RefreshTokenDays"] = "30",
                ["AccountManagement:TimingFloorMinimumMilliseconds"] = timingFloorMinimumMilliseconds.ToString(),
                ["AccountManagement:TimingFloorMaximumMilliseconds"] = timingFloorMaximumMilliseconds.ToString(),
            })
            .Build();

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options =>
                    {
                        // This host is a bearer-only API and never maps the session endpoint.
                        options.RequiresAuthSessionEndpoint = false;
                    });

                    services.RemoveAll<IEmailSender<TestUser>>();
                    if (emailSenderThrows)
                    {
                        services.AddSingleton<IEmailSender<TestUser>>(new ThrowingEmailSender<TestUser>());
                    }
                    else
                    {
                        services.AddSingleton<FakeEmailSender<TestUser>>();
                        services.AddSingleton<IEmailSender<TestUser>>(provider => provider.GetRequiredService<FakeEmailSender<TestUser>>());
                    }
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseBlazorBaseAccessTokenStampValidation();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapBlazorBaseAccountEndpoints<TestUser>());
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        return host;
    }

    /// <summary>
    /// Simulates a failing mail transport without a second host builder: registered instead of
    /// <see cref="FakeEmailSender{TUser}"/> when a test needs the send behind an account endpoint to
    /// fail.
    /// </summary>
    private sealed class ThrowingEmailSender<TUser> : IEmailSender<TUser>
        where TUser : BaseUser
    {
        public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink) =>
            throw new InvalidOperationException("Simulated mail transport failure.");

        public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink) =>
            throw new InvalidOperationException("Simulated mail transport failure.");

        public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode) =>
            throw new InvalidOperationException("Simulated mail transport failure.");
    }

    private static async Task<TestUser> SeedUserAsync(IHost host, string email, string password = "Secret123456")
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        var user = new TestUser { UserName = email, Email = email, DisplayName = "Test" };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        return user;
    }

    [Fact]
    public async Task ForgotPassword_AnswersByteIdentically_ForAKnownAndAnUnknownAddress()
    {
        using var host = await StartHostAsync();
        await SeedUserAsync(host, "known@example.test");

        using var client = host.GetTestClient();

        var knownResponse = await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "known@example.test" });
        var unknownResponse = await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "unknown@example.test" });

        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(await knownResponse.Content.ReadAsByteArrayAsync(), await unknownResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ForgotPassword_ReturnsAccepted_WhenTheEmailSenderThrows()
    {
        using var host = await StartHostAsync(timingFloorMinimumMilliseconds: 0, timingFloorMaximumMilliseconds: 0, emailSenderThrows: true);
        await SeedUserAsync(host, "known@example.test");

        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "known@example.test" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_ReturnsAccepted_WhenTheEmailSenderThrows()
    {
        using var host = await StartHostAsync(timingFloorMinimumMilliseconds: 0, timingFloorMaximumMilliseconds: 0, emailSenderThrows: true);
        await SeedUserAsync(host, "known@example.test");

        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/manage/resend-confirmation", new ResendConfirmationRequest { Email = "known@example.test" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_AnswersByteIdentically_ForAKnownAddressWhoseSendThrowsAndAnUnknownAddress()
    {
        using var host = await StartHostAsync(timingFloorMinimumMilliseconds: 0, timingFloorMaximumMilliseconds: 0, emailSenderThrows: true);
        await SeedUserAsync(host, "known@example.test");

        using var client = host.GetTestClient();

        var knownResponse = await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "known@example.test" });
        var unknownResponse = await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "unknown@example.test" });

        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(await knownResponse.Content.ReadAsByteArrayAsync(), await unknownResponse.Content.ReadAsByteArrayAsync());
    }

    private const int TimingFloorMilliseconds = 200;

    /// <summary>
    /// A small downward tolerance for timer granularity - the assertion still fails long before it
    /// could pass without the padding actually running, since the gap to the floor is two orders of
    /// magnitude larger than any granularity error this host would produce.
    /// </summary>
    private static readonly TimeSpan TimingFloorTolerance = TimeSpan.FromMilliseconds(15);

    [Fact]
    public async Task ForgotPassword_TakesAtLeastTheTimingFloor_ForAnUnknownAddress()
    {
        using var host = await StartHostAsync(TimingFloorMilliseconds, TimingFloorMilliseconds);
        using var client = host.GetTestClient();

        var stopwatch = Stopwatch.StartNew();
        await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "unknown@example.test" });
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(TimingFloorMilliseconds) - TimingFloorTolerance);
    }

    [Fact]
    public async Task ForgotPassword_TakesAtLeastTheTimingFloor_ForAKnownAddress()
    {
        using var host = await StartHostAsync(TimingFloorMilliseconds, TimingFloorMilliseconds);
        await SeedUserAsync(host, "known@example.test");
        using var client = host.GetTestClient();

        var stopwatch = Stopwatch.StartNew();
        await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "known@example.test" });
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(TimingFloorMilliseconds) - TimingFloorTolerance);
    }

    [Fact]
    public async Task ResendConfirmation_TakesAtLeastTheTimingFloor_ForAnUnknownAddress()
    {
        using var host = await StartHostAsync(TimingFloorMilliseconds, TimingFloorMilliseconds);
        using var client = host.GetTestClient();

        var stopwatch = Stopwatch.StartNew();
        await client.PostAsJsonAsync("/api/auth/manage/resend-confirmation", new ResendConfirmationRequest { Email = "unknown@example.test" });
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(TimingFloorMilliseconds) - TimingFloorTolerance);
    }

    [Fact]
    public async Task ForgotPassword_DoesNotPad_WhenTheTimingFloorIsDisabled()
    {
        using var host = await StartHostAsync();
        using var client = host.GetTestClient();

        var stopwatch = Stopwatch.StartNew();
        await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "unknown@example.test" });
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(TimingFloorMilliseconds));
    }

    [Fact]
    public async Task ForgotPassword_SendsAResetLink_ForAKnownAddressOnly()
    {
        using var host = await StartHostAsync();
        await SeedUserAsync(host, "known@example.test");

        using var client = host.GetTestClient();
        await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "known@example.test" });
        await client.PostAsJsonAsync("/api/auth/manage/forgot-password", new ForgotPasswordRequest { Email = "unknown@example.test" });

        using var scope = host.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<FakeEmailSender<TestUser>>();

        Assert.Single(sender.PasswordResetLinks);
        Assert.Equal("known@example.test", sender.PasswordResetLinks[0].Email);
    }

    [Fact]
    public async Task ResetPassword_CannotRedeemTheSameTokenTwice()
    {
        using var host = await StartHostAsync();
        await SeedUserAsync(host, "ada@example.test");

        string token;
        using (var scope = host.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
            var user = await userManager.FindByEmailAsync("ada@example.test");
            token = await userManager.GeneratePasswordResetTokenAsync(user!);
        }

        using var client = host.GetTestClient();

        await client.PostAsJsonAsync("/api/auth/manage/reset-password", new ResetPasswordRequest
        {
            Email = "ada@example.test",
            Token = token,
            NewPassword = "FirstNewPass1",
        });

        await client.PostAsJsonAsync("/api/auth/manage/reset-password", new ResetPasswordRequest
        {
            Email = "ada@example.test",
            Token = token,
            NewPassword = "SecondNewPass1",
        });

        using var scope2 = host.Services.CreateScope();
        var verifyingUserManager = scope2.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var reloaded = await verifyingUserManager.FindByEmailAsync("ada@example.test");

        Assert.True(await verifyingUserManager.CheckPasswordAsync(reloaded!, "FirstNewPass1"));
        Assert.False(await verifyingUserManager.CheckPasswordAsync(reloaded!, "SecondNewPass1"));
    }

    [Theory]
    [InlineData("GET", "/api/auth/manage/2fa")]
    [InlineData("POST", "/api/auth/manage/2fa/enable")]
    [InlineData("POST", "/api/auth/manage/2fa/disable")]
    [InlineData("POST", "/api/auth/manage/2fa/recovery-codes")]
    public async Task TwoFactorManagementEndpoints_RejectAnAnonymousCaller(string method, string path)
    {
        using var host = await StartHostAsync();
        using var client = host.GetTestClient();

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_Succeeds_WithAValidToken()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");

        string token;
        using (var scope = host.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
            token = await userManager.GenerateEmailConfirmationTokenAsync((await userManager.FindByIdAsync(user.Id))!);
        }

        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/manage/confirm-email", new ConfirmEmailRequest { UserId = user.Id, Token = token });

        response.EnsureSuccessStatusCode();

        using var scope2 = host.Services.CreateScope();
        var verifyingUserManager = scope2.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        Assert.True(await verifyingUserManager.IsEmailConfirmedAsync((await verifyingUserManager.FindByIdAsync(user.Id))!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("000000")]
    public async Task Disable_RejectsAMissingOrWrongCode_AndTwoFactorStaysEnabled(string code)
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        await EnableAuthenticatorTwoFactorAsync(host, user.Id);
        var accessToken = await IssueAccessTokenAsync(host, user);

        using var client = host.GetTestClient();
        Authenticate(client, accessToken);

        var response = await client.PostAsJsonAsync("/api/auth/manage/2fa/disable", new TwoFactorVerificationRequest { Code = code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        Assert.True(await userManager.GetTwoFactorEnabledAsync((await userManager.FindByIdAsync(user.Id))!));
    }

    [Fact]
    public async Task Disable_Succeeds_WithAValidTotpCode()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        var key = await EnableAuthenticatorTwoFactorAsync(host, user.Id);
        var accessToken = await IssueAccessTokenAsync(host, user);
        var code = TotpTestHelper.GenerateCode(key, DateTimeOffset.UtcNow);

        using var client = host.GetTestClient();
        Authenticate(client, accessToken);

        var response = await client.PostAsJsonAsync("/api/auth/manage/2fa/disable", new TwoFactorVerificationRequest { Code = code });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TwoFactorDisableResponse>();
        Assert.NotNull(body?.RefreshedTokens);

        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        Assert.False(await userManager.GetTwoFactorEnabledAsync((await userManager.FindByIdAsync(user.Id))!));
    }

    /// <summary>
    /// Disabling rotates the security stamp (<c>SetTwoFactorEnabledAsync</c> and
    /// <c>ResetAuthenticatorKeyAsync</c> both do) - the token used to call <c>disable</c> must die on
    /// its very next use, and the refreshed pair the response carries must authenticate in its place.
    /// </summary>
    [Fact]
    public async Task Disable_ReturnsARefreshedTokenPair_ThatAuthenticatesWhileThePreDisableTokenNoLongerDoes()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        var key = await EnableAuthenticatorTwoFactorAsync(host, user.Id);
        var preDisableAccessToken = await IssueAccessTokenAsync(host, user);
        var code = TotpTestHelper.GenerateCode(key, DateTimeOffset.UtcNow);

        using var client = host.GetTestClient();
        Authenticate(client, preDisableAccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/manage/2fa/disable", new TwoFactorVerificationRequest { Code = code });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TwoFactorDisableResponse>();
        var refreshedAccessToken = body?.RefreshedTokens?.AccessToken;
        Assert.False(string.IsNullOrEmpty(refreshedAccessToken));

        Authenticate(client, preDisableAccessToken);
        var withThePreDisableToken = await client.GetAsync("/api/auth/manage/2fa");
        Assert.Equal(HttpStatusCode.Unauthorized, withThePreDisableToken.StatusCode);

        Authenticate(client, refreshedAccessToken!);
        var withTheRefreshedToken = await client.GetAsync("/api/auth/manage/2fa");
        Assert.Equal(HttpStatusCode.OK, withTheRefreshedToken.StatusCode);
    }

    [Fact]
    public async Task Disable_Succeeds_WithAValidRecoveryCode()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        await EnableAuthenticatorTwoFactorAsync(host, user.Id);
        var accessToken = await IssueAccessTokenAsync(host, user);
        var recoveryCode = await GenerateRecoveryCodeAsync(host, user.Id);

        using var client = host.GetTestClient();
        Authenticate(client, accessToken);

        var response = await client.PostAsJsonAsync("/api/auth/manage/2fa/disable", new TwoFactorVerificationRequest { Code = recoveryCode });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TwoFactorDisableResponse>();
        Assert.NotNull(body?.RefreshedTokens);

        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        Assert.False(await userManager.GetTwoFactorEnabledAsync((await userManager.FindByIdAsync(user.Id))!));
    }

    [Fact]
    public async Task Disable_RejectsAnAlreadyUsedRecoveryCode()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        await EnableAuthenticatorTwoFactorAsync(host, user.Id);
        var accessToken = await IssueAccessTokenAsync(host, user);
        var recoveryCode = await GenerateRecoveryCodeAsync(host, user.Id);

        using var client = host.GetTestClient();
        Authenticate(client, accessToken);

        var firstAttempt = await client.PostAsJsonAsync("/api/auth/manage/2fa/disable", new TwoFactorVerificationRequest { Code = recoveryCode });
        Assert.Equal(HttpStatusCode.OK, firstAttempt.StatusCode);

        await EnableAuthenticatorTwoFactorAsync(host, user.Id);

        var accessTokenAfterStampRotatingReEnable = await IssueAccessTokenAsync(host, user);
        Authenticate(client, accessTokenAfterStampRotatingReEnable);

        var secondAttempt = await client.PostAsJsonAsync("/api/auth/manage/2fa/disable", new TwoFactorVerificationRequest { Code = recoveryCode });

        Assert.Equal(HttpStatusCode.BadRequest, secondAttempt.StatusCode);
    }

    /// <summary>
    /// <c>GET 2fa</c> generates the authenticator key the first time an account has none - a mutation
    /// Identity performs as a side effect of <c>ResetAuthenticatorKeyAsync</c>, which rotates the
    /// security stamp. The response must carry a refreshed token pair, and the token used to make this
    /// very call must die on its next use while the refreshed one authenticates in its place.
    /// </summary>
    [Fact]
    public async Task GetTwoFactorStatus_FirstCall_RotatesTheStamp_AndReturnsARefreshedTokenPairThatAuthenticates()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        var preStatusAccessToken = await IssueAccessTokenAsync(host, user);

        using var client = host.GetTestClient();
        Authenticate(client, preStatusAccessToken);

        var response = await client.GetAsync("/api/auth/manage/2fa");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        Assert.NotNull(body?.AuthenticatorUri);
        var refreshedAccessToken = body?.RefreshedTokens?.AccessToken;
        Assert.False(string.IsNullOrEmpty(refreshedAccessToken));

        Authenticate(client, preStatusAccessToken);
        var withThePreStatusToken = await client.GetAsync("/api/auth/manage/2fa");
        Assert.Equal(HttpStatusCode.Unauthorized, withThePreStatusToken.StatusCode);

        Authenticate(client, refreshedAccessToken!);
        var withTheRefreshedToken = await client.GetAsync("/api/auth/manage/2fa");
        Assert.Equal(HttpStatusCode.OK, withTheRefreshedToken.StatusCode);
    }

    /// <summary>
    /// Once the key exists, <c>GET 2fa</c> never rotates the stamp again - the second call must not
    /// carry a refreshed pair, and the very same access token must keep authenticating across it.
    /// </summary>
    [Fact]
    public async Task GetTwoFactorStatus_SecondCall_DoesNotRotateTheStamp_AndTheSameTokenKeepsWorking()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        var accessToken = await IssueAccessTokenAsync(host, user);

        using var client = host.GetTestClient();
        Authenticate(client, accessToken);

        var firstResponse = await client.GetAsync("/api/auth/manage/2fa");
        firstResponse.EnsureSuccessStatusCode();
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        var refreshedAccessToken = firstBody?.RefreshedTokens?.AccessToken;
        Assert.False(string.IsNullOrEmpty(refreshedAccessToken));

        Authenticate(client, refreshedAccessToken!);
        var secondResponse = await client.GetAsync("/api/auth/manage/2fa");
        secondResponse.EnsureSuccessStatusCode();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        Assert.Null(secondBody?.RefreshedTokens);

        var thirdResponse = await client.GetAsync("/api/auth/manage/2fa");
        Assert.Equal(HttpStatusCode.OK, thirdResponse.StatusCode);
    }

    /// <summary>
    /// Enabling rotates the security stamp too (<c>SetTwoFactorEnabledAsync</c>) - the token used to
    /// submit the enable code must die on its next use, and the refreshed pair the response carries
    /// must authenticate in its place.
    /// </summary>
    [Fact]
    public async Task Enable_RotatesTheStamp_AndReturnsARefreshedTokenPairThatAuthenticates()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        var loginAccessToken = await IssueAccessTokenAsync(host, user);

        using var client = host.GetTestClient();
        Authenticate(client, loginAccessToken);

        var statusResponse = await client.GetAsync("/api/auth/manage/2fa");
        statusResponse.EnsureSuccessStatusCode();
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        var secret = ExtractAuthenticatorSecret(statusBody!.AuthenticatorUri!);
        var accessTokenAfterKeyGeneration = statusBody.RefreshedTokens!.AccessToken;
        var code = TotpTestHelper.GenerateCode(secret, DateTimeOffset.UtcNow);

        Authenticate(client, accessTokenAfterKeyGeneration);
        var enableResponse = await client.PostAsJsonAsync("/api/auth/manage/2fa/enable", new TwoFactorEnableRequest { Code = code });
        enableResponse.EnsureSuccessStatusCode();
        var enableBody = await enableResponse.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesResponse>();
        var refreshedAccessToken = enableBody?.RefreshedTokens?.AccessToken;
        Assert.False(string.IsNullOrEmpty(refreshedAccessToken));

        Authenticate(client, accessTokenAfterKeyGeneration);
        var withThePreEnableToken = await client.GetAsync("/api/auth/manage/2fa");
        Assert.Equal(HttpStatusCode.Unauthorized, withThePreEnableToken.StatusCode);

        Authenticate(client, refreshedAccessToken!);
        var withTheRefreshedToken = await client.GetAsync("/api/auth/manage/2fa");
        Assert.Equal(HttpStatusCode.OK, withTheRefreshedToken.StatusCode);
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_RejectsAWrongCode()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        await EnableAuthenticatorTwoFactorAsync(host, user.Id);
        var accessToken = await IssueAccessTokenAsync(host, user);

        using var client = host.GetTestClient();
        Authenticate(client, accessToken);

        var response = await client.PostAsJsonAsync("/api/auth/manage/2fa/recovery-codes", new TwoFactorVerificationRequest { Code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_ReturnsAFreshBatch_WithAValidTotpCode()
    {
        using var host = await StartHostAsync();
        var user = await SeedUserAsync(host, "ada@example.test");
        var key = await EnableAuthenticatorTwoFactorAsync(host, user.Id);
        var accessToken = await IssueAccessTokenAsync(host, user);
        var code = TotpTestHelper.GenerateCode(key, DateTimeOffset.UtcNow);

        using var client = host.GetTestClient();
        Authenticate(client, accessToken);

        var response = await client.PostAsJsonAsync("/api/auth/manage/2fa/recovery-codes", new TwoFactorVerificationRequest { Code = code });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesResponse>();
        Assert.NotNull(body);
        Assert.Equal(10, body!.RecoveryCodes.Count);

        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        Assert.Equal(10, await userManager.CountRecoveryCodesAsync((await userManager.FindByIdAsync(user.Id))!));
    }

    /// <summary>
    /// Mirrors how the account actually enrols: reset the key so a TOTP code can be computed from it,
    /// then flip the flag <c>2fa/enable</c> would otherwise flip after verifying that same code.
    /// </summary>
    private static async Task<string> EnableAuthenticatorTwoFactorAsync(IHost host, string userId)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByIdAsync(userId);

        await userManager.ResetAuthenticatorKeyAsync(user!);
        var key = (await userManager.GetAuthenticatorKeyAsync(user!))!;
        await userManager.SetTwoFactorEnabledAsync(user!, true);

        return key;
    }

    private static async Task<string> GenerateRecoveryCodeAsync(IHost host, string userId)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByIdAsync(userId);
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user!, 5);

        return codes!.First();
    }

    private static string ExtractAuthenticatorSecret(string authenticatorUri) =>
        Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(authenticatorUri).Query)["secret"]!;


    /// <summary>
    /// Mints a real access token through the same issuer the login endpoint uses, since this test
    /// host maps only the account-management group and has no login endpoint of its own to call.
    /// Re-fetches the user by id first: <paramref name="user"/> is often the value returned by
    /// <see cref="SeedUserAsync"/> before <see cref="EnableAuthenticatorTwoFactorAsync"/> ran, and
    /// that call rotates the security stamp - minting from the stale in-memory copy would carry the
    /// pre-rotation stamp value into the token, exactly the situation a real login endpoint (which
    /// always loads the current user first) never produces.
    /// </summary>
    private static async Task<string> IssueAccessTokenAsync(IHost host, TestUser user)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>();

        var currentUser = await userManager.FindByIdAsync(user.Id);
        var principal = await signInManager.CreateUserPrincipalAsync(currentUser!);
        var tokens = await tokenIssuer.IssueTokenPairAsync(principal);

        return tokens.AccessToken;
    }

    private static void Authenticate(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
