using System.Net;
using System.Net.Http.Headers;
using BlazorBase.User.Server.AccessTokens;
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
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.Security;

/// <summary>
/// Proves, through a real <see cref="TestServer"/> round trip, that a bearer access token issued
/// before a password reset or a bare security-stamp rotation is rejected on its very next use (#26),
/// that an untouched session keeps working, that a configured interval trades freshness for load
/// exactly as documented, that disabling the check reproduces the pre-fix behavior, and that a machine
/// access token is left untouched.
/// </summary>
public sealed class AccessTokenStampValidationTests
{
    private static async Task<IHost> StartHostAsync(TimeSpan? securityStampValidationInterval = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configurationEntries = new Dictionary<string, string?>
        {
            ["AuthTokens:AccessTokenMinutes"] = "60",
            ["AuthTokens:RefreshTokenDays"] = "30",
            ["AccessTokenSettings:SecretPrefix"] = "test_",
            ["AccessTokenSettings:BruteForceBaseDelayMs"] = "0",
            ["AccessTokenSettings:BruteForceMaxDelayMs"] = "0",
        };

        if (securityStampValidationInterval is not null)
            configurationEntries["AuthTokens:SecurityStampValidationInterval"] = securityStampValidationInterval.Value.ToString();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationEntries)
            .Build();

        var timeProvider = new ManualTimeProvider();

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddSingleton<TimeProvider>(timeProvider);
                    services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options =>
                    {
                        options.RequiresAuthSessionEndpoint = false;
                    });
                    services.AddBlazorBaseAccessTokens<TestUserDbContext>(configuration);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseBlazorBaseAccessTokenStampValidation();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/protected", (HttpContext context) => context.User.Identity!.Name)
                            .RequireAuthorization();

                        endpoints.MapGet("/protected-machine", () => Results.Ok())
                            .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute
                            {
                                AuthenticationSchemes = AccessTokenAuthenticationDefaults.AuthenticationScheme,
                            });
                    });
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        return host;
    }

    private static ManualTimeProvider TimeProviderOf(IHost host) =>
        (ManualTimeProvider)host.Services.GetRequiredService<TimeProvider>();

    private static async Task<(TestUser User, string AccessToken)> SignInAsync(IHost host, string userName)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>();

        var user = new TestUser { UserName = userName, Email = $"{userName}@example.test" };
        var creationResult = await userManager.CreateAsync(user, "Secret123456");
        Assert.True(creationResult.Succeeded, string.Join("; ", creationResult.Errors.Select(e => e.Description)));

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var tokens = await tokenIssuer.IssueTokenPairAsync(principal);

        return (user, tokens.AccessToken);
    }

    private static async Task ResetPasswordAsync(IHost host, TestUser user)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var trackedUser = await userManager.FindByIdAsync(user.Id);
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(trackedUser!);
        var result = await userManager.ResetPasswordAsync(trackedUser!, resetToken, "NewSecret123456");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private static async Task RotateSecurityStampAsync(IHost host, TestUser user)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var trackedUser = await userManager.FindByIdAsync(user.Id);
        var result = await userManager.UpdateSecurityStampAsync(trackedUser!);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private static HttpRequestMessage ProtectedRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    [Fact]
    public async Task An_access_token_issued_before_a_password_reset_is_rejected_on_its_next_use()
    {
        using var host = await StartHostAsync();
        var (user, accessToken) = await SignInAsync(host, "reset-target");

        await ResetPasswordAsync(host, user);

        using var client = host.GetTestClient();
        var response = await client.SendAsync(ProtectedRequest(accessToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_access_token_issued_before_a_bare_stamp_rotation_is_rejected_on_its_next_use()
    {
        using var host = await StartHostAsync();
        var (user, accessToken) = await SignInAsync(host, "rotate-target");

        await RotateSecurityStampAsync(host, user);

        using var client = host.GetTestClient();
        var response = await client.SendAsync(ProtectedRequest(accessToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_untouched_session_keeps_authenticating_across_many_requests()
    {
        using var host = await StartHostAsync();
        var (_, accessToken) = await SignInAsync(host, "steady-state");

        using var client = host.GetTestClient();
        for (var requestNumber = 0; requestNumber < 5; requestNumber++)
        {
            var response = await client.SendAsync(ProtectedRequest(accessToken));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task With_a_non_zero_interval_a_rotation_is_not_observed_until_the_interval_elapses()
    {
        using var host = await StartHostAsync(TimeSpan.FromMinutes(5));
        var (user, accessToken) = await SignInAsync(host, "interval-target");

        using var client = host.GetTestClient();

        var beforeRotation = await client.SendAsync(ProtectedRequest(accessToken));
        Assert.Equal(HttpStatusCode.OK, beforeRotation.StatusCode);

        await RotateSecurityStampAsync(host, user);

        var withinInterval = await client.SendAsync(ProtectedRequest(accessToken));
        Assert.Equal(HttpStatusCode.OK, withinInterval.StatusCode);

        TimeProviderOf(host).Advance(TimeSpan.FromMinutes(6));

        var afterInterval = await client.SendAsync(ProtectedRequest(accessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, afterInterval.StatusCode);
    }

    [Fact]
    public async Task With_validation_disabled_a_rotated_stamp_does_not_invalidate_the_access_token()
    {
        using var host = await StartHostAsync(TimeSpan.FromSeconds(-1));
        var (user, accessToken) = await SignInAsync(host, "disabled-target");

        await RotateSecurityStampAsync(host, user);

        using var client = host.GetTestClient();
        var response = await client.SendAsync(ProtectedRequest(accessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_machine_access_token_authenticates_the_machine_scheme_endpoint_unaffected()
    {
        using var host = await StartHostAsync();

        using var scope = host.Services.CreateScope();
        var accessTokenService = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
        var created = await accessTokenService.CreateAsync("machine-caller", expiresAt: null);

        using var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected-machine");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.Secret);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
