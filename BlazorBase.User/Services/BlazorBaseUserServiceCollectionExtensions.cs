using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Services;

public static class BlazorBaseUserServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared client-side user/auth infrastructure:
    /// <see cref="BlazorBaseUserAuthStateProvider"/> as <see cref="AuthenticationStateProvider"/>,
    /// <see cref="CsrfTokenProvider"/>, <see cref="AuthTokenHandler"/> and
    /// <see cref="SessionExpiryHandler"/> as <see cref="DelegatingHandler"/>s for typed clients,
    /// <see cref="ILanguageService"/>, <see cref="IThemeService"/>, and the FallbackPolicy that
    /// requires authenticated users. Also registers <see cref="BlazorBaseUserClientOptions"/>
    /// with default values; override individual flags with
    /// <c>services.Configure&lt;BlazorBaseUserClientOptions&gt;(o =&gt; …)</c>.
    /// </summary>
    /// <remarks>
    /// Platform-specific dependencies (<see cref="ITokenStorage"/>, <see cref="IAppConfigService"/>)
    /// must be registered separately via AddBlazorBaseUserWasm or AddBlazorBaseUserMaui.
    /// <see cref="IAuthService"/> and <see cref="IUserService"/> must be registered by the caller
    /// using AddHttpClient&lt;IAuthService, AuthService&gt;(...) etc., because they need a host-configured
    /// <see cref="HttpClient"/>. <see cref="CsrfTokenProvider"/> is registered here rather than only
    /// under the cookie transport, because <see cref="BlazorBaseUserAuthStateProvider"/> reads the
    /// antiforgery token from every session response regardless of which transport is in use.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseUserClient(this IServiceCollection services)
    {
        services.AddAuthorizationCore(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        services.AddScoped<CsrfTokenProvider>();
        services.AddScoped<BlazorBaseUserAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<BlazorBaseUserAuthStateProvider>());

        services.AddScoped<AuthTokenHandler>();
        services.AddScoped<SessionExpiryHandler>();
        services.AddBlazorBaseComponents();
        services.AddScoped<IThemePreferenceSync, UserThemePreferenceSync>();

        services.AddOptions<BlazorBaseUserClientOptions>();

        return services;
    }
}
