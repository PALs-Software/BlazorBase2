using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Opt-in registration for the cookie (backend-for-frontend) token transport and its CSRF filter.
/// </summary>
public static class BlazorBaseAuthTokenTransportExtensions
{
    /// <summary>
    /// Switches token delivery to <c>HttpOnly</c> cookies and teaches the JWT handler to read the
    /// access token from one. Call after <c>AddBlazorBaseUserServer</c>.
    /// </summary>
    /// <remarks>
    /// Choose this when the client is a browser served from the same origin as the API. Leave it out
    /// for a native or service client, which needs the token in hand — see
    /// <see cref="BodyAuthTokenTransport"/>. The two are not exclusive at runtime: a bearer header
    /// always wins over the cookie, so a native client can keep using the exchange endpoint against a
    /// server configured this way.
    ///
    /// Cookies reintroduce CSRF. Apply <see cref="RequireBlazorBaseCsrf{TBuilder}"/> to every
    /// state-changing endpoint, and keep <c>GET</c> free of side effects.
    /// </remarks>
    /// <param name="schemeName">
    /// The JWT scheme the cookie fallback is attached to. Must be the same value passed to
    /// <c>AddBlazorBaseUserServer</c>; a host that renamed the scheme there and forgets it here gets a
    /// cookie nobody reads, and every request comes back unauthenticated with nothing in the log.
    /// </param>
    public static IServiceCollection AddBlazorBaseCookieAuthTokens(
        this IServiceCollection services,
        IConfiguration configuration,
        string schemeName = JwtBearerDefaults.AuthenticationScheme,
        Action<AuthCookieOptions>? configureOptions = null)
    {
        services.Configure<AuthCookieOptions>(configuration.GetSection(AuthCookieOptions.SectionName));

        if (configureOptions is not null)
            services.Configure(configureOptions);

        // Replaces rather than adds, so the outcome does not depend on whether this ran before or
        // after AddBlazorBaseUserServer registered the body default.
        services.RemoveAll<IAuthTokenTransport>();
        services.AddScoped<IAuthTokenTransport, CookieAuthTokenTransport>();
        services.AddScoped<BlazorBaseCsrfEndpointFilter>();

        var cookieOptions = new AuthCookieOptions();
        configuration.GetSection(AuthCookieOptions.SectionName).Bind(cookieOptions);
        configureOptions?.Invoke(cookieOptions);

        services.AddAntiforgery(antiforgeryOptions =>
        {
            antiforgeryOptions.HeaderName = cookieOptions.CsrfHeaderName;
            antiforgeryOptions.Cookie.SecurePolicy = cookieOptions.SecureOnly
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.None;
            antiforgeryOptions.Cookie.SameSite = cookieOptions.SameSite;
        });

        ReadAccessTokenFromCookie(services, schemeName);

        return services;
    }

    /// <summary>
    /// Requires a matching CSRF token and a same-origin fetch for state-changing verbs. A no-op for
    /// <c>GET</c>, <c>HEAD</c>, <c>OPTIONS</c> and <c>TRACE</c>.
    /// </summary>
    public static TBuilder RequireBlazorBaseCsrf<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder => endpointBuilder.FilterFactories.Add((_, next) => async invocationContext =>
        {
            var filter = invocationContext.HttpContext.RequestServices.GetRequiredService<BlazorBaseCsrfEndpointFilter>();
            return await filter.InvokeAsync(invocationContext, next);
        }));

        return builder;
    }

    /// <summary>
    /// Falls back to the access-token cookie only when no <c>Authorization</c> header was presented,
    /// so a bearer client keeps working unchanged against the same server.
    /// </summary>
    private static void ReadAccessTokenFromCookie(IServiceCollection services, string schemeName)
    {
        services.AddOptions<JwtBearerOptions>(schemeName)
            .Configure<IOptions<AuthCookieOptions>>((jwtOptions, cookieOptions) =>
            {
                var cookieName = cookieOptions.Value.AccessTokenCookieName;
                var existing   = jwtOptions.Events?.OnMessageReceived;

                jwtOptions.Events ??= new JwtBearerEvents();
                jwtOptions.Events.OnMessageReceived = async context =>
                {
                    if (existing is not null)
                        await existing(context);

                    if (!string.IsNullOrEmpty(context.Token))
                        return;

                    if (!string.IsNullOrEmpty(context.Request.Headers.Authorization.ToString()))
                        return;

                    if (context.Request.Cookies.TryGetValue(cookieName, out var accessToken) && !string.IsNullOrEmpty(accessToken))
                        context.Token = accessToken;
                };
            });
    }
}
