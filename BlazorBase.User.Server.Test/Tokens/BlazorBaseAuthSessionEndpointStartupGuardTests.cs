using System.Net.Http.Json;
using BlazorBase.User.Server.Security;
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
/// A security control that silently does nothing is worse than one that is absent: a bearer token is
/// as opaque to the client as a cookie is, so a host that forgets
/// <c>app.MapBlazorBaseAuthSessionEndpoint()</c> must find out on the very first request rather than
/// shipping a client that can never read its own claims - regardless of which token transport is in
/// play. A host that deliberately opts out (<c>RequiresAuthSessionEndpoint = false</c>) because no
/// BlazorBase client consumes the API must keep starting fine without the endpoint - unless it also
/// uses the cookie transport, whose own unconditional registration is not affected by that opt-out.
/// </summary>
public sealed class BlazorBaseAuthSessionEndpointStartupGuardTests
{
    private static async Task<IHost> StartHostAsync(
        bool useCookies,
        bool mapSessionEndpoint,
        bool? requiresAuthSessionEndpoint = null)
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
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options =>
                    {
                        if (requiresAuthSessionEndpoint is not null)
                            options.RequiresAuthSessionEndpoint = requiresAuthSessionEndpoint.Value;
                    });

                    if (useCookies)
                        services.AddBlazorBaseCookieAuthTokens(configuration);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseBlazorBaseAccessTokenStampValidation();

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
    public async Task A_bearer_host_with_default_options_and_no_session_endpoint_mapped_fails_loudly()
    {
        using var host = await StartHostAsync(useCookies: false, mapSessionEndpoint: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetTestClient().GetAsync("/protected"));

        Assert.Contains("MapBlazorBaseAuthSessionEndpoint", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bearer_host_that_opted_out_and_never_maps_the_endpoint_serves_requests_normally()
    {
        using var host = await StartHostAsync(useCookies: false, mapSessionEndpoint: false, requiresAuthSessionEndpoint: false);

        var response = await host.GetTestClient().GetAsync("/protected");

        response.EnsureSuccessStatusCode();
        Assert.Equal("reached", await response.Content.ReadFromJsonAsync<string>());
    }

    [Fact]
    public async Task A_bearer_host_with_default_options_that_maps_the_endpoint_serves_requests_normally()
    {
        using var host = await StartHostAsync(useCookies: false, mapSessionEndpoint: true);

        var response = await host.GetTestClient().GetAsync("/protected");

        response.EnsureSuccessStatusCode();
        Assert.Equal("reached", await response.Content.ReadFromJsonAsync<string>());
    }

    [Fact]
    public async Task A_cookie_transport_host_that_opted_out_and_forgets_the_endpoint_still_fails_loudly()
    {
        using var host = await StartHostAsync(useCookies: true, mapSessionEndpoint: false, requiresAuthSessionEndpoint: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.GetTestClient().GetAsync("/protected"));

        Assert.Contains("MapBlazorBaseAuthSessionEndpoint", exception.Message, StringComparison.Ordinal);
    }
}
