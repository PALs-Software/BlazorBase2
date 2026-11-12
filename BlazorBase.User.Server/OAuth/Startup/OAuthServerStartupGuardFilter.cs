using BlazorBase.User.Server.AccessTokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Server.OAuth.Startup;

/// <summary>
/// Refuses to build the request pipeline when <c>AddBlazorBaseOAuthServer</c> is registered without
/// <c>AddBlazorBaseAccessTokens</c>: every token the authorization server issues is an access-token row,
/// so without that subsystem nothing could mint or authenticate one, and the failure would otherwise
/// only show up as the first connector's token request answering 500.
/// </summary>
/// <remarks>
/// Checked while the pipeline is built rather than inside <c>AddBlazorBaseOAuthServer</c>, so the two
/// registrations may be made in either order.
/// </remarks>
internal sealed class OAuthServerStartupGuardFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        var serviceProviderIsService = app.ApplicationServices.GetService<IServiceProviderIsService>();

        if (serviceProviderIsService is not null && !serviceProviderIsService.IsService(typeof(IAccessTokenService)))
            throw new InvalidOperationException(
                "AddBlazorBaseOAuthServer() is registered, but AddBlazorBaseAccessTokens<TContext>() is not. " +
                "The OAuth authorization server issues ordinary access tokens and needs the access-token subsystem " +
                "to mint and authenticate them. Call services.AddBlazorBaseAccessTokens<TContext>(configuration) as well.");

        next(app);
    };
}
