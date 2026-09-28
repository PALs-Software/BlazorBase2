using BlazorBase.User.Server.AccessTokens.BruteForce;
using BlazorBase.User.Server.AccessTokens.Caching;
using BlazorBase.User.Server.AccessTokens.Generation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Extension methods that register the access-token security subsystem.
/// </summary>
public static class BlazorBaseAccessTokenServiceCollectionExtensions
{
    /// <summary>
    /// Registers the access-token services and adds the
    /// <see cref="AccessTokenAuthenticationDefaults.AuthenticationScheme"/> scheme <b>additively</b>:
    /// the application's default scheme (JWT, for browser sessions) is left untouched, and
    /// endpoints opt into token authentication individually via <c>RequireAuthorization</c>.
    /// Settings are bound from the <c>AccessTokenSettings</c> configuration section.
    /// </summary>
    /// <typeparam name="TContext">
    /// The application's <see cref="DbContext"/>. Its model must contain
    /// <see cref="Entities.AccessToken"/>, which it does automatically when it derives from
    /// <see cref="Data.BaseUserDbContext{TUser}"/>.
    /// </typeparam>
    public static IServiceCollection AddBlazorBaseAccessTokens<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        services.Configure<AccessTokenSettings>(configuration.GetSection("AccessTokenSettings"));
        services.AddSingleton<IValidateOptions<AccessTokenSettings>, AccessTokenSettingsValidator>();
        services.AddOptions<AccessTokenSettings>().ValidateOnStart();

        services.AddSingleton<IAccessTokenCache, AccessTokenCache>();
        services.AddSingleton<IBruteForceDelayService, BruteForceDelayService>();

        services.AddScoped<IAccessTokenSecretGenerator, AccessTokenSecretGenerator>();
        services.AddScoped<IAccessTokenService, AccessTokenService<TContext>>();

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AccessTokenAuthenticationHandler>(
                AccessTokenAuthenticationDefaults.AuthenticationScheme,
                null);

        return services;
    }

    /// <summary>
    /// Registers a scoped <see cref="IAccessTokenClaimsAugmentor"/> implementation so
    /// <see cref="AccessTokenAuthenticationHandler"/> appends its claims to every authenticated
    /// access-token principal. Call once per augmentor type; multiple augmentors are applied in
    /// registration order. Not registering any augmentor is a no-op - existing consumers of
    /// <see cref="AddBlazorBaseAccessTokens{TContext}"/> are unaffected until they opt in.
    /// </summary>
    public static IServiceCollection AddAccessTokenClaimsAugmentor<TAugmentor>(this IServiceCollection services)
        where TAugmentor : class, IAccessTokenClaimsAugmentor
    {
        services.AddScoped<IAccessTokenClaimsAugmentor, TAugmentor>();
        return services;
    }
}
