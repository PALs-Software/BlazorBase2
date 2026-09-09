using System.Net.Http.Json;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.Tokens;

/// <summary>
/// A security control that silently does nothing is worse than one that is absent: a host that
/// registers the cookie transport but forgets <c>app.MapBlazorBaseAuthSessionEndpoint()</c> must find
/// out on the very first request rather than shipping a client that can never read its own claims or
/// obtain an antiforgery token, and a bearer-only host that never registers the cookie transport must
/// keep starting fine without the endpoint at all.
/// </summary>
public sealed class BlazorBaseAuthSessionEndpointStartupGuardTests
{
    private static async Task<IHost> StartHostAsync(bool useCookies, bool mapSessionEndpoint)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // The test server speaks plain HTTP, so a Secure cookie would never come back.
                ["AuthCookies:SecureOnly"] = "false",
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

                    if (useCookies)
                        services.AddBlazorBaseCookieAuthTokens(configuration);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();

                    if (useCookies)
                        app.UseBlazorBaseCsrf();

                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        if (mapSessionEndpoint)
                            endpoints.MapBlazorBaseAuthSessionEndpoint();

                        endpoints.MapGet("/protected", () => Results.Ok("reached"));
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
    public async Task A_host_that_registers_the_cookie_transport_and_forgets_the_endpoint_fails_loudly()
    {
        using var host = await StartHostAsync(useCookies: true, mapSessionEndpoint: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetTestClient().GetAsync("/protected"));

        Assert.Contains("MapBlazorBaseAuthSessionEndpoint", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_host_that_registers_the_cookie_transport_and_maps_the_endpoint_serves_requests_normally()
    {
        using var host = await StartHostAsync(useCookies: true, mapSessionEndpoint: true);

        var response = await host.GetTestClient().GetAsync("/protected");

        response.EnsureSuccessStatusCode();
        Assert.Equal("reached", await response.Content.ReadFromJsonAsync<string>());
    }

    [Fact]
    public async Task A_bearer_only_host_that_never_registers_the_cookie_transport_serves_requests_normally()
    {
        using var host = await StartHostAsync(useCookies: false, mapSessionEndpoint: false);

        var response = await host.GetTestClient().GetAsync("/protected");

        response.EnsureSuccessStatusCode();
        Assert.Equal("reached", await response.Content.ReadFromJsonAsync<string>());
    }
}
