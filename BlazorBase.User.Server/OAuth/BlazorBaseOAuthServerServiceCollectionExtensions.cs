using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.Authorization;
using BlazorBase.User.Server.OAuth.ClientMetadataDocuments;
using BlazorBase.User.Server.OAuth.Clients;
using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Grants;
using BlazorBase.User.Server.OAuth.Maintenance;
using BlazorBase.User.Server.OAuth.Metadata;
using BlazorBase.User.Server.OAuth.Startup;
using BlazorBase.User.Server.OAuth.Tokens;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth;

/// <summary>
/// Registers the OAuth 2.1 authorization server that lets an OAuth client - a claude.ai custom
/// connector, for example - obtain an access token for a protected endpoint with the user's consent.
/// </summary>
public static class BlazorBaseOAuthServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the authorization server: options bound from the <c>OAuthServer</c> section and
    /// validated at startup, the client, grant and token services, the RFC 9728 challenge hook for the
    /// access-token scheme, and the cleanup service. Requires <c>AddBlazorBaseUserServer</c> and
    /// <c>AddBlazorBaseAccessTokens</c>; the latter is checked when the pipeline is built. Map the
    /// endpoints with <c>app.MapBlazorBaseOAuthServer()</c> and every protected endpoint with
    /// <c>app.MapBlazorBaseOAuthProtectedResource(path)</c>.
    /// </summary>
    /// <param name="configureOptions">Applied after the configuration section, for values computed in code.</param>
    public static IServiceCollection AddBlazorBaseOAuthServer<TUser, TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<OAuthServerOptions>? configureOptions = null)
        where TUser : BaseUser
        where TContext : BaseUserDbContext<TUser>
    {
        services.Configure<OAuthServerOptions>(configuration.GetSection(OAuthServerOptions.SectionName));

        if (configureOptions is not null)
            services.Configure(configureOptions);

        services.PostConfigure<OAuthServerOptions>(options =>
        {
            if (options.Scopes.Count == 0)
                options.Scopes[OAuthServerOptions.DefaultScope] = OAuthServerOptions.DefaultScopeDescription;
        });

        services.AddSingleton<IValidateOptions<OAuthServerOptions>, OAuthServerOptionsValidator>();
        services.AddOptions<OAuthServerOptions>().ValidateOnStart();

        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<BaseUserDbContext<TUser>>(serviceProvider => serviceProvider.GetRequiredService<TContext>());

        services.TryAddSingleton<OAuthProtectedResourceRegistry>();
        services.TryAddSingleton<OAuthIssuerResolver>();
        services.TryAddSingleton<OAuthMetadataDocumentBuilder>();
        services.TryAddSingleton<OAuthAuthorizationRequestProtector>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAccessTokenChallengeParameterProvider, OAuthProtectedResourceChallengeParameterProvider>());

        services.AddHttpClient(OAuthClientIdMetadataDocumentFetcher.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(OAuthClientIdMetadataDocumentHttpHandler.Create);
        services.TryAddSingleton<IOAuthClientIdMetadataDocumentFetcher, OAuthClientIdMetadataDocumentFetcher>();
        services.TryAddScoped<OAuthClientIdMetadataDocumentResolver<TUser>>();
        services.TryAddScoped<IOAuthClientService, OAuthClientService<TUser>>();
        services.TryAddScoped<OAuthClientAuthenticator>();
        services.TryAddScoped<OAuthGrantService<TUser>>();
        services.TryAddScoped<IOAuthGrantService>(serviceProvider => serviceProvider.GetRequiredService<OAuthGrantService<TUser>>());
        services.TryAddScoped<IOAuthAuthorizationEndpointHandler, OAuthAuthorizationEndpointHandler<TUser>>();
        services.TryAddScoped<IOAuthTokenEndpointHandler, OAuthTokenEndpointHandler<TUser>>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, OAuthServerStartupGuardFilter>());
        services.AddHostedService<OAuthCleanupService<TUser>>();

        return services;
    }
}
