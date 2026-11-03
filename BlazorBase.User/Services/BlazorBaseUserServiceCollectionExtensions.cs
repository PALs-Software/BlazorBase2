using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <see cref="CsrfTokenProvider"/> and <see cref="AuthenticationSessionState"/> are registered
    /// scoped with <c>TryAdd</c>; the WebAssembly and MAUI registrations replace them with one instance
    /// per process through <see cref="AddBlazorBaseUserSharedClientSession"/>, in either call order.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseUserClient(this IServiceCollection services)
    {
        services.AddAuthorizationCore(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        services.TryAddScoped<CsrfTokenProvider>();
        services.TryAddScoped<AuthenticationSessionState>();
        services.AddScoped<BlazorBaseUserAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<BlazorBaseUserAuthStateProvider>());

        services.AddScoped<AuthTokenHandler>();
        services.AddScoped<SessionExpiryHandler>();
        services.AddBlazorBaseComponents();
        services.AddScoped<IThemePreferenceSync, UserThemePreferenceSync>();

        services.AddOptions<BlazorBaseUserClientOptions>();

        return services;
    }

    /// <summary>
    /// Makes the client's session state — <see cref="CsrfTokenProvider"/> and
    /// <see cref="AuthenticationSessionState"/> — a single instance for the whole process.
    /// </summary>
    /// <remarks>
    /// IHttpClientFactory builds every <see cref="DelegatingHandler"/> in a dependency injection scope of
    /// its own, so with scoped registrations <c>CsrfHeaderHandler</c> read an antiforgery token nobody had
    /// set (every write answered 403), and a 401 seen by <see cref="SessionExpiryHandler"/> invalidated a
    /// provider no component listened to. Correct only where one process serves one user, which is why
    /// the WebAssembly and MAUI registrations call this and <see cref="AddBlazorBaseUserClient"/> does
    /// not. Never call it in a server-rendered host, where it would share one user's session with every
    /// other. Safe to call before or after <see cref="AddBlazorBaseUserClient"/>.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseUserSharedClientSession(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<CsrfTokenProvider, CsrfTokenProvider>());
        services.Replace(ServiceDescriptor.Singleton<AuthenticationSessionState, AuthenticationSessionState>());
        return services;
    }
}
