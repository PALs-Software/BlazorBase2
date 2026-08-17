using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.RateLimiting;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Endpoints;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace BlazorBase.User.Server.Services;

public static class BlazorBaseUserServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers Identity, JWT bearer authentication, authorization, the "BlazorBaseAuth" rate-limiter
    /// policy, the TokenService, the IBaseDataProvider&lt;UserModel&gt; for admin user management, and
    /// IHttpContextAccessor. JWT settings (Secret, Issuer, Audience, ExpirationMinutes,
    /// RefreshExpirationDays) are read from configuration under "JwtSettings"; rate-limit settings
    /// (PermitLimit, WindowSeconds) are read from "JwtSettings:RateLimit". The host must call
    /// <c>app.UseRateLimiter()</c> for the policy to take effect.
    /// </summary>
    public static IServiceCollection AddBlazorBaseUserServer<TUser, TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TUser : BaseUser, new()
        where TContext : BaseUserDbContext<TUser>
    {
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
            .AddDefaultTokenProviders();

        var jwtSecret = configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JwtSettings:Secret must be configured.");

        if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
            throw new InvalidOperationException("JwtSettings:Secret must be at least 32 bytes (256 bits) to safely sign HMAC-SHA256 tokens.");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["JwtSettings:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["JwtSettings:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization();

        var rateLimitPermitLimit = configuration.GetValue("JwtSettings:RateLimit:PermitLimit", 10);
        var rateLimitWindowSeconds = configuration.GetValue("JwtSettings:RateLimit:WindowSeconds", 60);

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

        services.AddScoped<TokenService<TUser>>();
        services.AddScoped<BaseUserDbContext<TUser>>(serviceProvider => serviceProvider.GetRequiredService<TContext>());
        services.AddScoped<IBaseDataProvider<UserModel>, UserDataProvider<TUser>>();

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
    /// <see cref="TokenService{TUser}"/> appends its claims to every issued access token.
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
}
