using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Endpoints;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    /// IBaseDataProvider&lt;UserModel&gt; for admin user management, and IHttpContextAccessor. Token
    /// lifetimes (AccessTokenMinutes, RefreshTokenDays) are read from configuration under
    /// "AuthTokens"; rate-limit settings (PermitLimit, WindowSeconds) are read from
    /// "AuthTokens:RateLimit". The host must call <c>app.UseRateLimiter()</c> for the policy to take
    /// effect.
    /// </summary>
    /// <param name="configureOptions">
    /// Optionally overrides the bearer scheme name and whether it becomes the authentication default -
    /// see <see cref="BlazorBaseUserServerOptions"/>. Leave <see langword="null"/> to keep today's
    /// behavior: scheme name <see cref="IdentityConstants.BearerScheme"/>, claiming the default.
    /// </param>
    public static IServiceCollection AddBlazorBaseUserServer<TUser, TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<BlazorBaseUserServerOptions>? configureOptions = null)
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
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
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

        services.AddScoped<AuthTokenIssuer<TUser>>();
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
