using System.Net;
using System.Net.Http.Headers;
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

namespace BlazorBase.User.Server.Test.Tokens;

/// <summary>
/// Proves, through a real <see cref="TestServer"/> round trip rather than by assumption, that a token
/// minted by <see cref="AuthTokenIssuer"/> and delivered by <c>IdentityBearerAuthTransport</c> actually
/// authenticates a subsequent request.
/// </summary>
/// <remarks>
/// This is the check the migration to Identity bearer tokens hinges on: signing in to the bearer
/// scheme directly would have <c>BearerTokenHandler.HandleSignInAsync</c> write an
/// <c>AccessTokenResponse</c> straight to the response body instead, which no status-code-only test
/// would catch. Minting through the scheme's own protectors and sending the result as an
/// <c>Authorization: Bearer</c> header is what this test exercises end to end.
/// </remarks>
public sealed class IdentityBearerAuthTransportRoundTripTests
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
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options =>
                    {
                        // This host is a bearer-only API and never maps the session endpoint.
                        options.RequiresAuthSessionEndpoint = false;
                    });
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/protected", (HttpContext context) => context.User.Identity!.Name)
                            .RequireAuthorization();
                    });
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        return host;
    }

    [Fact]
    public async Task A_token_minted_by_the_transport_authenticates_a_protected_request()
    {
        using var host = await StartHostAsync();

        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>();

        var user = new TestUser { UserName = "round-trip@example.test", Email = "round-trip@example.test" };
        var creationResult = await userManager.CreateAsync(user, "Secret123456");
        Assert.True(creationResult.Succeeded, string.Join("; ", creationResult.Errors.Select(e => e.Description)));

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var tokens = await tokenIssuer.IssueTokenPairAsync(principal);

        using var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("round-trip@example.test", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_unauthenticated_request_to_the_protected_endpoint_is_rejected()
    {
        using var host = await StartHostAsync();

        using var client = host.GetTestClient();
        var response = await client.GetAsync("/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_garbage_bearer_token_is_rejected()
    {
        using var host = await StartHostAsync();

        using var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
