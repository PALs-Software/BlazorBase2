using BlazorBase.User.Server.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Extension methods that register external-token (OIDC) validation for the exchange endpoint
/// mapped by <c>MapBlazorBaseExternalAuthEndpoints</c>.
/// </summary>
public static class BlazorBaseExternalAuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IExternalTokenValidator"/>. Settings are bound from the
    /// "ExternalAuthSettings" configuration section and then, if given, overridden by
    /// <paramref name="configureOptions"/> - useful for a host that derives its authority/audience
    /// from other configuration (e.g. an existing <c>AzureAd:ClientId</c> key) instead of a static
    /// value in <c>appsettings.json</c>.
    /// </summary>
    /// <remarks>
    /// The host must additionally register its own <see cref="IExternalUserResolver{TUser}"/> and
    /// call <c>MapBlazorBaseExternalAuthEndpoints</c> - there is no default resolver and no
    /// endpoint is mapped by this call alone, so an application that does not opt into all three
    /// sees no change at all. A host that needs its allowed tenants to come from a database
    /// rather than the static <see cref="ExternalAuthSettings.AllowedTenantIds"/> list must
    /// register its own <see cref="IExternalTenantAllowList"/> before calling this method, since
    /// the default registration below uses <c>TryAdd…</c> and only applies when nothing else has
    /// claimed the service yet.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseExternalAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ExternalAuthSettings>? configureOptions = null)
    {
        services.Configure<ExternalAuthSettings>(configuration.GetSection(ExternalAuthSettings.SectionName));

        if (configureOptions is not null)
            services.Configure(configureOptions);

        services.TryAddSingleton<IExternalTenantAllowList, DefaultExternalTenantAllowList>();
        services.AddSingleton<IValidateOptions<ExternalAuthSettings>, ExternalAuthSettingsValidator>();
        services.AddOptions<ExternalAuthSettings>().ValidateOnStart();

        services.AddSingleton<IExternalTokenValidator, OidcExternalTokenValidator>();

        // The endpoints take a transport from DI; default it here so mapping them without calling
        // AddBlazorBaseUserServer first still resolves.
        services.TryAddScoped<IAuthTokenTransport, IdentityBearerAuthTransport>();

        return services;
    }
}
