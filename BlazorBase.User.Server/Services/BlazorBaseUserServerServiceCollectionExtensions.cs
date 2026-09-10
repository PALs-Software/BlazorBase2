using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Endpoints;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.ExternalAuth;
using BlazorBase.User.Server.Security;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BlazorBase.User.Server.Services;

public static class BlazorBaseUserServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers Identity (including <see cref="SignInManager{TUser}"/> and the claims-principal
    /// factory that builds every issued token's claims), the Identity bearer-token scheme,
    /// authorization, the "BlazorBaseAuth" rate-limiter policy, <see cref="AuthTokenIssuer{TUser}"/>, the
    /// IBaseDataProvider&lt;UserModel&gt; for admin user management, IHttpContextAccessor, and a default
    /// <see cref="IExternalUserResolver{TUser}"/> (<see cref="IdentityLoginExternalUserResolver{TUser}"/>,
    /// registered with <c>TryAddScoped</c> so a host's own registration - made before or after this call
    /// - always wins). Token lifetimes (AccessTokenMinutes, RefreshTokenDays) are read from configuration
    /// under "AuthTokens"; rate-limit settings (PermitLimit, WindowSeconds) are read from
    /// "AuthTokens:RateLimit". The host must call <c>app.UseRateLimiter()</c> for the policy to take
    /// effect.
    /// </summary>
    /// <param name="configureOptions">
    /// Optionally overrides the bearer scheme name, whether it becomes the authentication default, and
    /// whether the session endpoint is required - see <see cref="BlazorBaseUserServerOptions"/>. Leave
    /// <see langword="null"/> to keep today's behavior: scheme name
    /// <see cref="IdentityConstants.BearerScheme"/>, claiming the default, session endpoint required.
    /// </param>
    /// <param name="configurePassword">
    /// Applied after the framework's own password-policy defaults, inside the same
    /// <c>AddIdentityCore</c> configuration delegate - so a host's choice always wins regardless of
    /// whether <c>services.Configure&lt;IdentityOptions&gt;</c> is called before or after this method,
    /// which a second, independently-ordered <c>Configure</c> call could not guarantee. Leave
    /// <see langword="null"/> to keep the framework's own policy: length 12, a digit, a lowercase and
    /// an uppercase letter, no symbol required.
    /// </param>
    public static IServiceCollection AddBlazorBaseUserServer<TUser, TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<BlazorBaseUserServerOptions>? configureOptions = null,
        Action<PasswordOptions>? configurePassword = null)
        where TUser : BaseUser, new()
        where TContext : BaseUserDbContext<TUser>
    {
        var schemeOptions = new BlazorBaseUserServerOptions();
        configureOptions?.Invoke(schemeOptions);

        // Registered so later opt-ins - the cookie token transport in particular - can target the
        // scheme name this call actually used instead of assuming the default.
        services.AddSingleton(schemeOptions);
        services.TryAddScoped<IAuthTokenTransport, IdentityBearerAuthTransport>();

        // Resolved by AuthTokenIssuer rather than assumed system time, so a test can swap in a
        // controllable clock without waiting on a real one.
        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpContextAccessor();

        services.AddIdentityCore<TUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                // 12, not the ASP.NET Core default of 6: length is what actually resists guessing,
                // and a longer minimum costs an attacker far more than another composition rule -
                // see NIST SP 800-63B. Existing stored hashes are unaffected; this only changes what
                // a *new* password must satisfy.
                options.Password.RequiredLength = 12;
                options.User.RequireUniqueEmail = true;

                configurePassword?.Invoke(options.Password);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TContext>()
            .AddDefaultTokenProviders()
            // AddIdentityCore omits SignInManager, but lockout and two-factor detection live there.
            .AddSignInManager()
            // One claim set for the bearer token and any later cookie sign-in alike.
            .AddClaimsPrincipalFactory<BlazorBaseUserClaimsPrincipalFactory<TUser>>();

        // AddIdentityCore().AddSignInManager() does not register these - only the full
        // AddIdentity<TUser, TRole>() does. Without them, AddIdentityCookies()'s OnValidatePrincipal
        // event throws the first time an authenticated request resolves ISecurityStampValidator from DI.
        services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<TUser>>();
        services.TryAddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<TUser>>();

        // Set deliberately rather than left at the default; it only bites once a cookie transport exists.
        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromMinutes(5);
        });

        var accessTokenMinutes = configuration.GetValue("AuthTokens:AccessTokenMinutes", AuthTokenDefaults.AccessTokenExpirationMinutes);
        var refreshTokenDays = configuration.GetValue("AuthTokens:RefreshTokenDays", AuthTokenDefaults.RefreshTokenExpirationDays);

        services.AddAuthentication(authOptions =>
            {
                if (!schemeOptions.ClaimDefaultScheme)
                    return;

                authOptions.DefaultAuthenticateScheme = schemeOptions.SchemeName;
                authOptions.DefaultChallengeScheme = schemeOptions.SchemeName;
            })
            .AddBearerToken(schemeOptions.SchemeName, options =>
            {
                options.BearerTokenExpiration = TimeSpan.FromMinutes(accessTokenMinutes);
                options.RefreshTokenExpiration = TimeSpan.FromDays(refreshTokenDays);
            });

        services.AddAuthorization();

        var securityStampValidationInterval = configuration.GetValue("AuthTokens:SecurityStampValidationInterval", AuthTokenDefaults.SecurityStampValidationInterval);
        var stampValidationOptions = new AccessTokenStampValidationOptions { Interval = securityStampValidationInterval };

        services.TryAddSingleton(stampValidationOptions);
        services.TryAddSingleton<AccessTokenStampValidationCache>();
        services.TryAddScoped<IAccessTokenStampValidator, AccessTokenStampValidator<TUser>>();

        // Only when this call claims the authentication default: a host that dispatches its own
        // schemes (ClaimDefaultScheme = false) has declared it manages that wiring itself - see
        // BlazorBaseAccessTokenStampValidationStartupGuardFilter.
        if (schemeOptions.ClaimDefaultScheme && stampValidationOptions.Enabled)
        {
            services.TryAddSingleton<BlazorBaseAccessTokenStampValidationMiddlewareRegistration>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, BlazorBaseAccessTokenStampValidationStartupGuardFilter>());
        }

        var rateLimitPermitLimit = configuration.GetValue("AuthTokens:RateLimit:PermitLimit", 10);
        var rateLimitWindowSeconds = configuration.GetValue("AuthTokens:RateLimit:WindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy("BlazorBaseAuth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ResolveRateLimitPartitionKey(httpContext.Connection.RemoteIpAddress),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitWindowSeconds),
                    QueueLimit = 0,
                }));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        // Always registered: the rate limiter above is unconditional, so a host that never calls
        // AddBlazorBaseForwardedHeaders is warned rather than left silently partitioning every
        // client behind its proxy into one bucket - see BlazorBaseForwardedHeadersStartupGuardFilter.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, BlazorBaseForwardedHeadersStartupGuardFilter>());

        // AddBlazorBaseCookieAuthTokens registers the same pair unconditionally - TryAdd/TryAddEnumerable
        // dedupe by service type and implementation type respectively, so a host that uses both transports
        // ends up with exactly one registration of each regardless of call order.
        if (schemeOptions.RequiresAuthSessionEndpoint)
        {
            services.TryAddSingleton<BlazorBaseAuthSessionEndpointRegistration>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, BlazorBaseAuthSessionEndpointStartupGuardFilter>());
        }

        // Every api/auth/* response carries a bearer token, a refresh token, or account state that
        // must not be replayed from a shared cache or a browser back-button history entry.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, BlazorBaseAuthNoStoreStartupFilter>());

        services.AddScoped<AuthTokenIssuer<TUser>>();
        services.TryAddScoped<IExternalUserResolver<TUser>, IdentityLoginExternalUserResolver<TUser>>();
        services.TryAddSingleton<ITwoFactorPendingTokenStore, InMemoryTwoFactorPendingTokenStore>();
        services.TryAddSingleton(typeof(TwoFactorPendingTokenService<>));
        services.TryAddScoped(typeof(IEmailSender<>), typeof(NoOpEmailSender<>));
        services.Configure<AccountManagementOptions>(configuration.GetSection(AccountManagementOptions.SectionName));
        services.AddScoped<BaseUserDbContext<TUser>>(serviceProvider => serviceProvider.GetRequiredService<TContext>());
        services.AddScoped<IBaseDataProvider<UserModel>, UserDataProvider<TUser>>();

        var cleanupIntervalHours = configuration.GetValue("AuthTokens:CleanupIntervalHours", AuthTokenDefaults.CleanupIntervalHours);
        services.TryAddSingleton(new RefreshTokenCleanupOptions { Interval = TimeSpan.FromHours(cleanupIntervalHours) });
        services.AddHostedService<RefreshTokenCleanupService<TUser>>();

        return services;
    }

    /// <summary>
    /// Resolves the rate-limiter partition key for a client IP address. Genuine IPv6 addresses are
    /// masked to their /64 prefix (the last 8 bytes zeroed) so a single attacker controlling a whole
    /// /64 cannot obtain a fresh bucket per address; IPv4 addresses (including IPv4-mapped IPv6, as seen
    /// on a dual-stack socket) keep their full address so IPv4 clients are not collapsed into one
    /// bucket; a missing address falls back to a shared key.
    /// </summary>
    private static string ResolveRateLimitPartitionKey(IPAddress? address)
    {
        if (address is null)
            return "unknown";

        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4().ToString();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        var addressBytes = address.GetAddressBytes();
        for (var byteIndex = 8; byteIndex < addressBytes.Length; byteIndex++)
            addressBytes[byteIndex] = 0;

        return new IPAddress(addressBytes).ToString();
    }

    /// <summary>
    /// Configures <see cref="ForwardedHeadersOptions"/> from <c>ForwardedHeaders:KnownProxies</c> and
    /// <c>ForwardedHeaders:KnownNetworks</c>, and records that the choice was made so
    /// <see cref="BlazorBaseForwardedHeadersStartupGuardFilter"/> stops warning about it. Call
    /// before <c>app.UseForwardedHeaders()</c> whenever this app sits behind a reverse proxy - the
    /// "BlazorBaseAuth" rate limiter and <see cref="AccessTokens.BruteForce.BruteForceDelayService"/>
    /// both key on <c>HttpContext.Connection.RemoteIpAddress</c>, which is the proxy's own address,
    /// identical for every client, until forwarded headers are processed.
    /// </summary>
    /// <remarks>
    /// Deliberately does not fall back to an unrestricted <see cref="ForwardedHeadersOptions.KnownIPNetworks"/>
    /// or <see cref="ForwardedHeadersOptions.KnownProxies"/> entry when configuration supplies neither:
    /// an unrestricted trust list lets any caller supply its own <c>X-Forwarded-For</c> value and pick
    /// its own rate-limit partition, which is worse than today's single shared bucket, because the
    /// attacker - not the network topology - would then choose the key. With nothing configured, this
    /// method still registers <see cref="ForwardedHeadersOptions"/>, but with no trusted proxy or
    /// network, so <see cref="ForwardedHeadersMiddleware"/> forwards nothing and
    /// <c>RemoteIpAddress</c> stays exactly what it was before this method existed.
    /// </remarks>
    public static IServiceCollection AddBlazorBaseForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("ForwardedHeaders");
        var knownProxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
        var knownNetworks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var proxy in knownProxies)
            {
                if (IPAddress.TryParse(proxy, out var address))
                    options.KnownProxies.Add(address);
            }

            foreach (var network in knownNetworks)
            {
                if (System.Net.IPNetwork.TryParse(network, out var ipNetwork))
                    options.KnownIPNetworks.Add(ipNetwork);
            }
        });

        // A plain Add, not TryAdd: this must win regardless of whether it runs before or after
        // AddBlazorBaseUserServer, and the last registration of a singleton type is the one
        // GetService resolves.
        services.AddSingleton(new ForwardedHeadersConfigurationMarker(knownProxies.Length > 0 || knownNetworks.Length > 0));

        return services;
    }


    private static bool TryParseNetwork(string value, out IPAddress prefix, out int prefixLength)
    {
        prefix = IPAddress.None;
        prefixLength = 0;

        var parts = value.Split('/', 2);
        if (parts.Length != 2)
            return false;

        if (!IPAddress.TryParse(parts[0], out var address))
            return false;

        if (!int.TryParse(parts[1], out var length))
            return false;

        prefix = address;
        prefixLength = length;
        return true;
    }

    /// <summary>
    /// Opts the host into development authentication: with
    /// <c>DevelopmentAuthentication:Enabled</c> set, the client signs itself in as the configured
    /// account through <c>POST api/auth/development-login</c> and no login form appears. The
    /// account is a real Identity user and receives a normal access token, so the authorization
    /// pipeline stays the production one. Change <c>DevelopmentAuthentication:Roles</c> and
    /// restart to try the app as a different role.
    /// </summary>
    /// <remarks>
    /// Safe to call unconditionally: the feature stays off unless the configuration switches it
    /// on. If it is switched on outside the Development environment this <b>throws at startup</b>
    /// rather than silently opening a passwordless door — a failed deployment is preferable to an
    /// unauthenticated production app.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The option is enabled while <paramref name="environment"/> is not Development.
    /// </exception>
    public static IServiceCollection AddBlazorBaseDevelopmentAuthentication<TUser>(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
        where TUser : BaseUser, new()
    {
        var section = configuration.GetSection(DevelopmentAuthenticationOptions.SectionName);
        services.Configure<DevelopmentAuthenticationOptions>(section);

        if (section.GetValue("Enabled", false) && !environment.IsDevelopment())
            throw new InvalidOperationException(
                $"{DevelopmentAuthenticationOptions.SectionName}:Enabled is set in the '{environment.EnvironmentName}' environment. " +
                "Development authentication signs users in without a password and must never leave Development. " +
                "Remove the setting from this environment's configuration.");

        services.AddScoped<IDevelopmentSessionService<TUser>, DevelopmentSessionService<TUser>>();

        return services;
    }

    /// <summary>
    /// Registers a scoped <see cref="IClaimsAugmentor{TUser}"/> implementation so
    /// <see cref="BlazorBaseUserClaimsPrincipalFactory{TUser}"/> appends its claims to every issued
    /// access token.
    /// Call once per augmentor type; multiple augmentors are applied in registration order.
    /// </summary>
    public static IServiceCollection AddClaimsAugmentor<TUser, TAugmentor>(this IServiceCollection services)
        where TUser : BaseUser
        where TAugmentor : class, IClaimsAugmentor<TUser>
    {
        services.AddScoped<IClaimsAugmentor<TUser>, TAugmentor>();
        return services;
    }

    /// <summary>
    /// Maps the BlazorBase.CRUD endpoints for UserModel under the "users" path,
    /// restricted to the Admin role.
    /// </summary>
    public static IEndpointRouteBuilder MapBlazorBaseUserAdminEndpoints(
        this IEndpointRouteBuilder endpoints,
        string path = "users",
        string roles = "Admin")
    {
        endpoints.MapBaseEndpoints<UserModel>(path, options => options.Roles = roles);
        return endpoints;
    }

    /// <summary>
    /// Replaces the default <see cref="NoOpEmailSender{TUser}"/> with an adapter onto
    /// <see cref="BlazorBase.Mailing.Services.IEmailSender"/>, so account-management email travels
    /// through the framework's existing mail path instead of a second one. Requires
    /// <c>AddBlazorBaseMailing</c> (or an equivalent <c>IEmailSender</c> registration) to already be
    /// registered.
    /// </summary>
    public static IServiceCollection AddBlazorBaseMailingEmailSender<TUser>(this IServiceCollection services)
        where TUser : BaseUser
    {
        services.Replace(ServiceDescriptor.Scoped<IEmailSender<TUser>, MailingEmailSender<TUser>>());
        return services;
    }
}
