using System.Net;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.Extensions;

/// <summary>
/// Pins what <see cref="BlazorBaseUserServerServiceCollectionExtensions.AddBlazorBaseForwardedHeaders"/>
/// actually trusts. The configured entries are added to ASP.NET Core's defaults rather than replacing
/// them, so loopback stays trusted whatever is configured: a proxy on the same machine works without
/// configuration, and an empty list does not mean "forward nothing".
/// </summary>
public sealed class ForwardedHeadersLoopbackDefaultTests
{
    private const string ForwardedClientAddress = "198.51.100.7";
    private const string ExternalCallerAddress = "203.0.113.10";
    private const string ConfiguredProxyAddress = "10.0.0.4";

    private static async Task<IHost> StartHostAsync(string? knownProxy)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(knownProxy is null
                ? []
                : new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = knownProxy })
            .Build();

        return await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services => services.AddBlazorBaseForwardedHeaders(configuration));
                webHost.Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["X-Test-Caller"].ToString());
                        await next();
                    });
                    app.UseForwardedHeaders();
                    app.Run(context => context.Response.WriteAsync(context.Connection.RemoteIpAddress?.ToString() ?? string.Empty));
                });
            })
            .StartAsync();
    }

    private static async Task<string> SeenAddressAsync(IHost host, string callerAddress)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Test-Caller", callerAddress);
        request.Headers.Add("X-Forwarded-For", ForwardedClientAddress);

        using var response = await host.GetTestClient().SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task With_nothing_configured_a_loopback_proxy_is_still_trusted()
    {
        using var host = await StartHostAsync(knownProxy: null);

        Assert.Equal(ForwardedClientAddress, await SeenAddressAsync(host, "127.0.0.1"));
    }

    [Fact]
    public async Task With_nothing_configured_an_external_caller_cannot_choose_its_address()
    {
        using var host = await StartHostAsync(knownProxy: null);

        Assert.Equal(ExternalCallerAddress, await SeenAddressAsync(host, ExternalCallerAddress));
    }

    [Fact]
    public async Task A_configured_proxy_is_trusted_in_addition_to_loopback()
    {
        using var host = await StartHostAsync(ConfiguredProxyAddress);

        Assert.Equal(ForwardedClientAddress, await SeenAddressAsync(host, ConfiguredProxyAddress));
        Assert.Equal(ForwardedClientAddress, await SeenAddressAsync(host, "127.0.0.1"));
    }
}
