using BlazorBase.User.Server.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Opt-in registration for the cookie (backend-for-frontend) token transport and its CSRF filter.
/// </summary>
public static class BlazorBaseAuthTokenTransportExtensions
{
    /// <summary>
    /// The scheme a bearer request and a cookie request are both authenticated against. Forwards to
    /// whichever of the two actually applies to the request, so a single default scheme can serve
    /// both kinds of client — see <see cref="AddBlazorBaseCookieAuthTokens"/>.
    /// </summary>
    /// <remarks>
    /// Public because a host that kept its own authentication default
    /// (<c>BlazorBaseUserServerOptions.ClaimDefaultScheme = false</c>) has to be able to name this
    /// scheme when wiring it into its own policy scheme.
    /// </remarks>
    public const string BearerOrCookieScheme = "BlazorBaseBearerOrCookie";

    /// <summary>
    /// Switches token delivery to ASP.NET Core Identity's own application cookie and teaches the
    /// authentication default to serve a bearer request and a cookie request alike. Call after
    /// <c>AddBlazorBaseUserServer</c>.
    /// </summary>
    /// <remarks>
    /// Choose this when the client is a browser served from the same origin as the API. Leave it out
    /// for a native or service client, which needs the token in hand — see
    /// <see cref="IdentityBearerAuthTransport"/>. The two are not exclusive at runtime: a bearer header
    /// always wins over the cookie, so a native client can keep using the exchange endpoint against a
    /// server configured this way.
    ///
    /// Cookies reintroduce CSRF. Apply <see cref="RequireBlazorBaseCsrf{TBuilder}"/> to every
    /// state-changing endpoint, and keep <c>GET</c> free of side effects.
    /// </remarks>
    /// <param name="bearerSchemeName">
    /// The JWT scheme a bearer request is forwarded to. Leave <see langword="null"/> to reuse the name
    /// <c>AddBlazorBaseUserServer</c> actually registered, which is almost always what is wanted;
    /// passing a name that was never registered sends every bearer request into nothing.
    /// </param>
    public static IServiceCollection AddBlazorBaseCookieAuthTokens(
        this IServiceCollection services,
        IConfiguration configuration,
        string? bearerSchemeName = null,
        Action<AuthCookieOptions>? configureOptions = null)
    {
        // Registered as an instance by AddBlazorBaseUserServer, so the choices made there are
        // readable here instead of having to be repeated by the caller.
        var serverOptions = services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(BlazorBaseUserServerOptions))?
            .ImplementationInstance as BlazorBaseUserServerOptions;

        var resolvedBearerScheme = bearerSchemeName
            ?? serverOptions?.SchemeName
            ?? IdentityConstants.BearerScheme;

        services.Configure<AuthCookieOptions>(configuration.GetSection(AuthCookieOptions.SectionName));

        if (configureOptions is not null)
            services.Configure(configureOptions);

        // Replaces rather than adds, so the outcome does not depend on whether this ran before or
        // after AddBlazorBaseUserServer registered the body default.
        services.RemoveAll<IAuthTokenTransport>();
        services.AddScoped<IAuthTokenTransport, IdentityCookieAuthTransport>();
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

        services.AddAuthentication(authOptions =>
            {
                // A host that kept its own default did so deliberately; overriding it here would
                // silently disable the policy scheme it dispatches with.
                if (serverOptions?.ClaimDefaultScheme == false)
                    return;

                authOptions.DefaultAuthenticateScheme = BearerOrCookieScheme;
                authOptions.DefaultChallengeScheme = BearerOrCookieScheme;
            })
            .AddPolicyScheme(BearerOrCookieScheme, "Bearer token or Identity session cookie", policySchemeOptions =>
            {
                policySchemeOptions.ForwardDefaultSelector = httpContext =>
                    string.IsNullOrEmpty(httpContext.Request.Headers.Authorization.ToString())
                        ? IdentityConstants.ApplicationScheme
                        : resolvedBearerScheme;
            })
            .AddIdentityCookies();

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IServiceProvider>((cookieAuthOptions, rootServiceProvider) =>
            {
                cookieAuthOptions.Cookie.Name = cookieOptions.EffectiveCookieName;
                cookieAuthOptions.Cookie.HttpOnly = true;
                cookieAuthOptions.Cookie.SecurePolicy = cookieOptions.SecureOnly
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.None;
                cookieAuthOptions.Cookie.SameSite = cookieOptions.SameSite;
                cookieAuthOptions.ExpireTimeSpan = cookieOptions.ExpireTimeSpan;
                cookieAuthOptions.SlidingExpiration = true;

                // Resolved from the root provider rather than assumed system time, so a test can swap
                // in a controllable clock without waiting on a real one.
                cookieAuthOptions.TimeProvider = rootServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

                // This is an API: a browser client reads the status code and decides what to do,
                // rather than following a redirect to a login page that does not exist here.
                cookieAuthOptions.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                cookieAuthOptions.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

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
}

