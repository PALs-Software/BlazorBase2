using System.Net;
using System.Net.Http.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Account;
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
    private static async Task<IHost> StartHostAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthTokens:AccessTokenMinutes"] = "15",
                ["AuthTokens:RefreshTokenDays"] = "30",
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
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration);

                    services.RemoveAll<IEmailSender<TestUser>>();
                    services.AddSingleton<FakeEmailSender<TestUser>>();
                    services.AddSingleton<IEmailSender<TestUser>>(provider => provider.GetRequiredService<FakeEmailSender<TestUser>>());
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
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
}
