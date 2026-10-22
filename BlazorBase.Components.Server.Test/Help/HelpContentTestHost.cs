using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BlazorBase.Components.Server.Test.Help;

/// <summary>A <see cref="TestServer"/> hosting <c>MapBlazorBaseHelpContent</c> behind the authentication of the test.</summary>
internal sealed class HelpContentTestHost : IDisposable
{
    public const string EditorsPolicy = "Editors";
    public const string EditorUser = "editor";

    private HelpContentTestHost(IHost host) => Host = host;

    public IHost Host { get; }

    public static async Task<HelpContentTestHost> StartAsync(Action<IEndpointRouteBuilder> mapHelp, bool useCookieAuthentication = false)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    var authentication = services.AddAuthentication(useCookieAuthentication ? CookieAuthenticationDefaults.AuthenticationScheme : TestUserAuthenticationHandler.SchemeName);

                    if (useCookieAuthentication)
                        authentication.AddCookie();
                    else
                        authentication.AddScheme<AuthenticationSchemeOptions, TestUserAuthenticationHandler>(TestUserAuthenticationHandler.SchemeName, _ => { });

                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy(EditorsPolicy, policy => policy.RequireClaim(ClaimTypes.Name, EditorUser));
                        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                    });
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(mapHelp);
                });
            })
            .StartAsync();

        return new HelpContentTestHost(host);
    }

    public HttpClient CreateClient() => Host.GetTestClient();

    public async Task<HttpContext> SendRawAsync(string path, bool signedIn = true)
    {
        return await Host.GetTestServer().SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = path;

            if (signedIn)
                context.Request.Headers[TestUserAuthenticationHandler.UserHeader] = "alice";
        });
    }

    public void Dispose() => Host.Dispose();
}
