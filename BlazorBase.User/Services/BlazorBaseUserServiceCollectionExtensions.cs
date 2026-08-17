using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Services;

public static class BlazorBaseUserServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared client-side user/auth infrastructure:
    /// <see cref="BlazorBaseUserAuthStateProvider"/> as <see cref="AuthenticationStateProvider"/>,
    /// <see cref="AuthTokenHandler"/> as a <see cref="DelegatingHandler"/> for typed clients,
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
    /// <see cref="HttpClient"/>.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseUserClient(this IServiceCollection services)
    {
        services.AddAuthorizationCore(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        services.AddScoped<BlazorBaseUserAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<BlazorBaseUserAuthStateProvider>());

        services.AddScoped<AuthTokenHandler>();
        services.AddScoped<ILanguageService, LanguageService>();
        services.AddScoped<IThemeService, ThemeService>();

        services.AddOptions<BlazorBaseUserClientOptions>();

        return services;
    }
}
