using BlazorBase.User.Server.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Opt-in registration for the cookie (backend-for-frontend) token transport, its antiforgery
/// service, and the CSRF middleware that guards it.
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
    /// Cookies reintroduce CSRF. Call <see cref="UseBlazorBaseCsrf"/> on the pipeline, after
    /// <c>app.UseAuthentication()</c>, and keep <c>GET</c> free of side effects. Forgetting the call is
    /// not silent — see <see cref="BlazorBaseCsrfStartupGuardFilter"/>.
    ///
    /// Also registers <see cref="BlazorBaseTicketStore"/> as the default <c>ITicketStore</c> and wires
    /// it into the application cookie's <c>SessionStore</c>, so sign-out revokes the session
    /// server-side instead of merely asking the browser to forget the cookie — see
    /// <see cref="AuthCookieOptions.ServerSideSessionStoreEnabled"/> to opt out.
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

        // Flipped by UseBlazorBaseCsrf() and read by the startup guard below, so a host that forgets
        // the call fails loudly instead of quietly shipping an unprotected cookie transport.
        services.TryAddSingleton<BlazorBaseCsrfMiddlewareRegistration>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, BlazorBaseCsrfStartupGuardFilter>());

        // Flipped by MapBlazorBaseAuthSessionEndpoint() and read by the startup guard below, so a host
        // that forgets to map the endpoint fails loudly instead of quietly shipping a client that can
        // never read its own claims or obtain an antiforgery token.
        services.TryAddSingleton<BlazorBaseAuthSessionEndpointRegistration>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, BlazorBaseAuthSessionEndpointStartupGuardFilter>());

        services.TryAddSingleton<ITicketStore, BlazorBaseTicketStore>();

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

                if (cookieOptions.ServerSideSessionStoreEnabled)
                    cookieAuthOptions.SessionStore = rootServiceProvider.GetRequiredService<ITicketStore>();

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
    /// No longer needed: CSRF protection is applied globally by <see cref="UseBlazorBaseCsrf"/> once
    /// the middleware is wired into the pipeline, and that also covers the MVC controller actions this
    /// per-endpoint opt-in could never reach. Kept as a no-op, rather than removed outright, so a host
    /// that still calls it does not fail to build.
    /// </summary>
    [Obsolete("CSRF protection is now applied by the app.UseBlazorBaseCsrf() middleware for every state-changing request, including MVC controller actions. This call is a no-op and can be removed.")]
    public static TBuilder RequireBlazorBaseCsrf<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder;

    /// <summary>
    /// Enforces <see cref="BlazorBaseCsrfMiddleware"/> for every state-changing request from this
    /// point in the pipeline onward. Place it after <c>app.UseAuthentication()</c> — the antiforgery
    /// token is bound to the caller's identity, so it can only be validated once that identity has
    /// been resolved — and before <c>app.UseAuthorization()</c> and whatever maps controllers or
    /// minimal API endpoints.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="AddBlazorBaseCookieAuthTokens"/> was never called — this middleware has nothing to
    /// protect without the cookie transport's antiforgery registration.
    /// </exception>
    public static IApplicationBuilder UseBlazorBaseCsrf(this IApplicationBuilder app)
    {
        var registration = app.ApplicationServices.GetService<BlazorBaseCsrfMiddlewareRegistration>()
            ?? throw new InvalidOperationException(
                "app.UseBlazorBaseCsrf() requires AddBlazorBaseCookieAuthTokens() to have been called first.");

        registration.WasRegistered = true;

        return app.UseMiddleware<BlazorBaseCsrfMiddleware>();
    }

    /// <summary>
    /// Opts an endpoint out of <see cref="BlazorBaseCsrfMiddleware"/> — see
    /// <see cref="AllowBlazorBaseCsrfAttribute"/> for when that is warranted. Apply to nothing by
    /// default.
    /// </summary>
    public static TBuilder AllowBlazorBaseCsrf<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new AllowBlazorBaseCsrfAttribute());
        return builder;
    }
}

