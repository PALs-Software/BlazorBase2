using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlazorBase.Mailing;
using BlazorBase.Mailing.Models;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Account;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Security;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;
using MailingEmailSender = BlazorBase.Mailing.Services.IEmailSender;

namespace BlazorBase.User.Server.Test.Account;

/// <summary>
/// End-to-end coverage for <c>POST change-password</c> through a real <see cref="TestServer"/> under the
/// bearer transport: the password changes only for the right current password and a password the rules
/// accept, every refusal has its documented status and body, the security stamp rotation ends the other
/// access tokens and refresh tokens while the caller's refreshed pair keeps working, and the notification
/// mail goes out through the mailing sender - and only there. The cookie transport is covered in
/// <c>CookieAuthTokenTransportTests</c>.
/// </summary>
public sealed class ChangePasswordEndpointTests
{
    private const string Path = "/api/auth/manage/change-password";
    private const string TwoFactorProbePath = "/api/auth/manage/2fa";
    private const string OldPassword = "Secret123456";
    private const string NewPassword = "BrandNewSecret123456";

    private enum NotificationMode
    {
        NoOp,
        Mailing,
        Throwing,
    }

    private sealed class ThrowingPasswordChangedNotifier<TUser> : IPasswordChangedNotifier<TUser>
        where TUser : BaseUser
    {
        public Task NotifyPasswordChangedAsync(TUser user, string email, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated mail transport failure.");
    }

    private sealed record TestHostContext(IHost Host, RecordingMailingEmailSender SentEmails);

    private static async Task<TestHostContext> StartHostAsync(NotificationMode mode = NotificationMode.NoOp, int rateLimitPermitLimit = 1000)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthTokens:AccessTokenMinutes"] = "15",
                ["AuthTokens:RefreshTokenDays"] = "30",
                ["AuthTokens:RateLimit:PermitLimit"] = rateLimitPermitLimit.ToString(),
                ["EmailBranding:BrandName"] = "TestBrand",
            })
            .Build();

        var sentEmails = new RecordingMailingEmailSender();

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options => options.RequiresAuthSessionEndpoint = false);

                    services.AddBlazorBaseMailing(configuration, dispatchMode: EmailDispatchMode.Inline);
                    services.RemoveAll<MailingEmailSender>();
                    services.AddSingleton<MailingEmailSender>(sentEmails);

                    if (mode == NotificationMode.Mailing)
                        services.AddBlazorBaseMailingEmailSender<TestUser>();

                    if (mode == NotificationMode.Throwing)
                        services.AddScoped<IPasswordChangedNotifier<TestUser>, ThrowingPasswordChangedNotifier<TestUser>>();
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
        await scope.ServiceProvider.GetRequiredService<TestUserDbContext>().Database.EnsureCreatedAsync();

        return new TestHostContext(host, sentEmails);
    }

    private static async Task<TestUser> SeedUserAsync(IHost host, string? password = OldPassword, string language = "en")
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        var user = new TestUser { UserName = "ada@example.test", Email = "ada@example.test", DisplayName = "Ada", Language = language };
        var result = password is null ? await userManager.CreateAsync(user) : await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));

        return user;
    }

    private static async Task<LoginResponse> IssueTokenPairAsync(IHost host, string userId)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>();

        var principal = await signInManager.CreateUserPrincipalAsync((await userManager.FindByIdAsync(userId))!);
        return await tokenIssuer.IssueTokenPairAsync(principal);
    }

    private static HttpClient CreateClient(IHost host, string? accessToken)
    {
        var client = host.GetTestClient();

        if (accessToken is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, string currentPassword, string newPassword) =>
        client.PostAsJsonAsync(Path, new ChangePasswordRequest { CurrentPassword = currentPassword, NewPassword = newPassword });

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement;

    private static async Task<TestUser> ReloadAsync(IHost host, string userId)
    {
        using var scope = host.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>().FindByIdAsync(userId))!;
    }

    private static async Task<bool> CheckPasswordAsync(IHost host, string userId, string password)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        return await userManager.CheckPasswordAsync((await userManager.FindByIdAsync(userId))!, password);
    }

    private static async Task<bool> HasPasswordAsync(IHost host, string userId)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        return await userManager.HasPasswordAsync((await userManager.FindByIdAsync(userId))!);
    }

    [Fact]
    public async Task Success_ChangesThePassword_AndReturnsARefreshedTokenPair()
    {
        var context = await StartHostAsync();
        using var host = context.Host;
        var user = await SeedUserAsync(host);
        var tokens = await IssueTokenPairAsync(host, user.Id);

        var response = await ChangeAsync(CreateClient(host, tokens.AccessToken), OldPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        Assert.False(string.IsNullOrEmpty(body?.RefreshedTokens?.AccessToken));
        Assert.False(string.IsNullOrEmpty(body?.RefreshedTokens?.RefreshToken));
        Assert.True(await CheckPasswordAsync(host, user.Id, NewPassword));
        Assert.False(await CheckPasswordAsync(host, user.Id, OldPassword));
    }

    [Fact]
    public async Task Success_RotatesTheStamp_SoOtherSessionsEnd_AndTheCallersRefreshedPairKeepsWorking()
    {
        var context = await StartHostAsync();
        using var host = context.Host;
        var user = await SeedUserAsync(host);
        var callerTokens = await IssueTokenPairAsync(host, user.Id);
        var otherDeviceTokens = await IssueTokenPairAsync(host, user.Id);
        var stampBefore = (await ReloadAsync(host, user.Id)).SecurityStamp;

        var response = await ChangeAsync(CreateClient(host, callerTokens.AccessToken), OldPassword, NewPassword);
        response.EnsureSuccessStatusCode();
        var refreshedTokens = (await response.Content.ReadFromJsonAsync<ChangePasswordResponse>())!.RefreshedTokens!;

        Assert.NotEqual(stampBefore, (await ReloadAsync(host, user.Id)).SecurityStamp);

        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient(host, callerTokens.AccessToken).GetAsync(TwoFactorProbePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient(host, otherDeviceTokens.AccessToken).GetAsync(TwoFactorProbePath)).StatusCode);

        using var scope = host.Services.CreateScope();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>();

        var callerRefresh = await tokenIssuer.FindAsync(callerTokens.RefreshToken);
        var otherDeviceRefresh = await tokenIssuer.FindAsync(otherDeviceTokens.RefreshToken);
        var refreshedRefresh = await tokenIssuer.FindAsync(refreshedTokens.RefreshToken);

        Assert.True(callerRefresh!.IsRevoked);
        Assert.True(otherDeviceRefresh!.IsRevoked);
        Assert.Null(await tokenIssuer.ValidateSecurityStampAsync(otherDeviceRefresh));
        Assert.False(refreshedRefresh!.IsRevoked);
        Assert.NotNull(await tokenIssuer.ValidateSecurityStampAsync(refreshedRefresh));
        Assert.Equal(HttpStatusCode.OK, (await CreateClient(host, refreshedTokens.AccessToken).GetAsync(TwoFactorProbePath)).StatusCode);
    }

    [Fact]
    public async Task WrongCurrentPassword_Returns400WithAGenericCode_AndChangesNothing()
    {
        var context = await StartHostAsync();
        using var host = context.Host;
        var user = await SeedUserAsync(host);
        var tokens = await IssueTokenPairAsync(host, user.Id);
        var stampBefore = (await ReloadAsync(host, user.Id)).SecurityStamp;

        var response = await ChangeAsync(CreateClient(host, tokens.AccessToken), "Not-The-Password-1", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal(ChangePasswordErrorCodes.CurrentPasswordIncorrect, body.GetProperty("code").GetString());
        Assert.Equal("The current password is incorrect.", body.GetProperty("message").GetString());
        Assert.False(body.TryGetProperty("errors", out _));
        Assert.True(await CheckPasswordAsync(host, user.Id, OldPassword));
        Assert.Equal(stampBefore, (await ReloadAsync(host, user.Id)).SecurityStamp);
        Assert.Equal(HttpStatusCode.OK, (await CreateClient(host, tokens.AccessToken).GetAsync(TwoFactorProbePath)).StatusCode);
    }

    [Fact]
    public async Task WeakNewPassword_Returns400WithFieldKeyedErrors_AndChangesNothing()
    {
        var context = await StartHostAsync();
        using var host = context.Host;
        var user = await SeedUserAsync(host);
        var tokens = await IssueTokenPairAsync(host, user.Id);

        var response = await ChangeAsync(CreateClient(host, tokens.AccessToken), OldPassword, "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal(ChangePasswordErrorCodes.ValidationFailed, body.GetProperty("code").GetString());
        var reasons = body.GetProperty("errors").GetProperty("newPassword").EnumerateArray().Select(reason => reason.GetString()).ToList();
        Assert.NotEmpty(reasons);
        Assert.Contains(reasons, reason => reason!.Contains("at least 12", StringComparison.Ordinal));
        Assert.False(body.GetProperty("errors").TryGetProperty("currentPassword", out _));
        Assert.True(await CheckPasswordAsync(host, user.Id, OldPassword));
        Assert.Equal(HttpStatusCode.OK, (await CreateClient(host, tokens.AccessToken).GetAsync(TwoFactorProbePath)).StatusCode);
    }

    [Theory]
    [InlineData("", NewPassword, "currentPassword")]
    [InlineData(OldPassword, "", "newPassword")]
    public async Task AMissingPassword_Returns400WithAFieldError(string currentPassword, string newPassword, string expectedField)
    {
        var context = await StartHostAsync();
        using var host = context.Host;
        var user = await SeedUserAsync(host);
        var tokens = await IssueTokenPairAsync(host, user.Id);

        var response = await ChangeAsync(CreateClient(host, tokens.AccessToken), currentPassword, newPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal(ChangePasswordErrorCodes.ValidationFailed, body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty(expectedField, out _));
    }

    [Fact]
    public async Task AnAccountWithoutAPassword_Returns400NoPasswordSet()
    {
        var context = await StartHostAsync();
        using var host = context.Host;
        var user = await SeedUserAsync(host, password: null);
        var tokens = await IssueTokenPairAsync(host, user.Id);

        var response = await ChangeAsync(CreateClient(host, tokens.AccessToken), OldPassword, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal(ChangePasswordErrorCodes.NoPasswordSet, body.GetProperty("code").GetString());
        Assert.Contains("forgot password", body.GetProperty("message").GetString());
        Assert.False(await HasPasswordAsync(host, user.Id));
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var context = await StartHostAsync();
        using var host = context.Host;
        await SeedUserAsync(host);

        var response = await ChangeAsync(CreateClient(host, accessToken: null), OldPassword, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(context.SentEmails.Messages);
    }

    [Fact]
    public async Task AGarbageBearerToken_Returns401()
    {
        var context = await StartHostAsync();
        using var host = context.Host;
        await SeedUserAsync(host);

        var response = await ChangeAsync(CreateClient(host, "not-a-token"), OldPassword, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheEndpointIsOnTheAuthRateLimit()
    {
        var context = await StartHostAsync(rateLimitPermitLimit: 2);
        using var host = context.Host;
        var client = CreateClient(host, accessToken: null);

        var statusCodes = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 3; attempt++)
            statusCodes.Add((await ChangeAsync(client, OldPassword, NewPassword)).StatusCode);

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statusCodes);
    }

    [Fact]
    public async Task Success_SendsThePasswordChangedMail_ToTheAccount_InItsLanguage()
    {
        var context = await StartHostAsync(NotificationMode.Mailing);
        using var host = context.Host;
        var user = await SeedUserAsync(host, language: "de");
        var tokens = await IssueTokenPairAsync(host, user.Id);

        var response = await ChangeAsync(CreateClient(host, tokens.AccessToken), OldPassword, NewPassword);

        response.EnsureSuccessStatusCode();
        var message = Assert.Single(context.SentEmails.Messages);
        Assert.Equal(["ada@example.test"], message.To);
        Assert.Equal("Ihr Passwort für TestBrand wurde geändert", message.Subject);
        Assert.Contains("Guten Tag Ada,", message.HtmlBody);
        Assert.DoesNotContain("href=", message.HtmlBody);
        Assert.DoesNotContain(NewPassword, message.HtmlBody);
        Assert.DoesNotContain(OldPassword, message.HtmlBody);
    }

    [Fact]
    public async Task Success_SendsTheEnglishMail_ForAnEnglishAccount()
    {
        var context = await StartHostAsync(NotificationMode.Mailing);
        using var host = context.Host;
        var user = await SeedUserAsync(host);
        var tokens = await IssueTokenPairAsync(host, user.Id);

        (await ChangeAsync(CreateClient(host, tokens.AccessToken), OldPassword, NewPassword)).EnsureSuccessStatusCode();

        var message = Assert.Single(context.SentEmails.Messages);
        Assert.Equal("Your TestBrand password was changed", message.Subject);
        Assert.Contains("Hello Ada,", message.HtmlBody);
    }

    [Theory]
    [InlineData("Not-The-Password-1", NewPassword)]
    [InlineData(OldPassword, "short")]
    public async Task ARefusedChange_SendsNoMail(string currentPassword, string newPassword)
    {
        var context = await StartHostAsync(NotificationMode.Mailing);
        using var host = context.Host;
        var user = await SeedUserAsync(host);
        var tokens = await IssueTokenPairAsync(host, user.Id);

        var response = await ChangeAsync(CreateClient(host, tokens.AccessToken), currentPassword, newPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(context.SentEmails.Messages);
    }

    [Fact]
    public async Task WithTheNoOpDefault_NothingIsSent_AndTheChangeSucceeds()
    {
        var context = await StartHostAsync(NotificationMode.NoOp);
        using var host = context.Host;
        var user = await SeedUserAsync(host);
        var tokens = await IssueTokenPairAsync(host, user.Id);

        var response = await ChangeAsync(CreateClient(host, tokens.AccessToken), OldPassword, NewPassword);

        response.EnsureSuccessStatusCode();
        Assert.Empty(context.SentEmails.Messages);
        using var scope = host.Services.CreateScope();
        Assert.IsType<NoOpPasswordChangedNotifier<TestUser>>(scope.ServiceProvider.GetRequiredService<IPasswordChangedNotifier<TestUser>>());
        Assert.True(await CheckPasswordAsync(host, user.Id, NewPassword));
    }

    [Fact]
    public async Task AFailingNotification_IsSwallowed_AndTheRequestStillSucceeds()
    {
        var context = await StartHostAsync(NotificationMode.Throwing);
        using var host = context.Host;
        var user = await SeedUserAsync(host);
        var tokens = await IssueTokenPairAsync(host, user.Id);

        var response = await ChangeAsync(CreateClient(host, tokens.AccessToken), OldPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        Assert.False(string.IsNullOrEmpty(body?.RefreshedTokens?.AccessToken));
        Assert.True(await CheckPasswordAsync(host, user.Id, NewPassword));
    }

    [Fact]
    public async Task AddBlazorBaseMailingEmailSender_ReplacesTheNoOpNotifier()
    {
        var context = await StartHostAsync(NotificationMode.Mailing);
        using var host = context.Host;

        using var scope = host.Services.CreateScope();

        Assert.IsType<MailingPasswordChangedNotifier<TestUser>>(scope.ServiceProvider.GetRequiredService<IPasswordChangedNotifier<TestUser>>());
    }
}
