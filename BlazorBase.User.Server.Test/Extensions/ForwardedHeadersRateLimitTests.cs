using System.Net;
using BlazorBase.User.Server.Security;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
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
/// Proves that the "BlazorBaseAuth" rate limiter partitions by the real client address, not the
/// reverse proxy's, but only once <see cref="BlazorBaseUserServerServiceCollectionExtensions.AddBlazorBaseForwardedHeaders"/>
/// names that proxy as trusted - and that with nothing configured, behaviour is unchanged from
/// before forwarded-header support existed: every caller collapses into one shared bucket.
/// </summary>
public sealed class ForwardedHeadersRateLimitTests
{
    // TestServer's own transport-level caller is loopback, which ASP.NET Core's forwarded-headers
    // defaults already treat as a known proxy - too well-known to prove anything here. A fixed,
    // clearly-external address stands in for "whatever hop the test server would otherwise report",
    // set by a tiny middleware ahead of app.UseForwardedHeaders(), so the "not configured" case can
    // actually be *not* configured instead of accidentally inheriting the loopback default.
    private const string SimulatedDirectCallerAddress = "203.0.113.10";

    private static async Task<IHost> StartHostAsync(bool trustTheProxy)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configurationEntries = new Dictionary<string, string?>
        {
            ["AuthTokens:AccessTokenMinutes"] = "15",
            ["AuthTokens:RefreshTokenDays"] = "30",
            ["AuthTokens:RateLimit:PermitLimit"] = "1",
            ["AuthTokens:RateLimit:WindowSeconds"] = "60",
        };

        if (trustTheProxy)
            configurationEntries["ForwardedHeaders:KnownProxies:0"] = SimulatedDirectCallerAddress;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationEntries)
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
                    services.AddBlazorBaseForwardedHeaders(configuration);
                });
                webHost.Configure(app =>
                {
                    app.Use(async (httpContext, next) =>
                    {
                        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(SimulatedDirectCallerAddress);
                        await next(httpContext);
                    });
                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseBlazorBaseAccessTokenStampValidation();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/limited", () => Results.Ok())
                                 .RequireRateLimiting("BlazorBaseAuth");
                    });
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TestUserDbContext>().Database.EnsureCreatedAsync();

        return host;
    }

    private static async Task<HttpResponseMessage> RequestFromAsync(IHost host, string forwardedForAddress)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/limited");
        request.Headers.Add("X-Forwarded-For", forwardedForAddress);
        return await host.GetTestClient().SendAsync(request);
    }

    [Fact]
    public async Task Different_forwarded_addresses_land_in_different_partitions_when_the_proxy_is_known()
    {
        using var host = await StartHostAsync(trustTheProxy: true);

        var first = await RequestFromAsync(host, "1.2.3.4");
        var second = await RequestFromAsync(host, "5.6.7.8");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Different_forwarded_addresses_share_one_partition_when_no_proxy_is_configured()
    {
        using var host = await StartHostAsync(trustTheProxy: false);

        var first = await RequestFromAsync(host, "1.2.3.4");
        var second = await RequestFromAsync(host, "5.6.7.8");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }
}
