using BlazorBase.User.Server.Test.Infrastructure;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.Extensions;

/// <summary>
/// Every <c>api/auth/*</c> response must refuse caching (#18): it carries a bearer token, a refresh
/// token, or the session endpoint's claim list, none of which may be served from a shared cache or
/// replayed from browser history.
/// </summary>
public sealed class AuthNoStoreHeaderTests
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
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapBlazorBaseAuthSessionEndpoint();
                        endpoints.MapGet("/notes", () => Results.Ok("fine to cache"));
                    });
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TestUserDbContext>().Database.EnsureCreatedAsync();

        return host;
    }

    [Fact]
    public async Task The_session_endpoint_is_marked_no_store()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/auth/session");

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task An_endpoint_outside_api_auth_is_left_untouched()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/notes");

        Assert.DoesNotContain("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
    }
}
