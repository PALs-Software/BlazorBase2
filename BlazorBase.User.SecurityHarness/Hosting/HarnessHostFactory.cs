using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.Server.Account;
using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.ExternalAuth;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>
/// Starts one real Kestrel host per <see cref="HarnessProfile"/>, binding to an operating-system
/// assigned loopback port and reading the actual address back afterwards, so parallel runs never
/// collide on a fixed port.
/// </summary>
public static class HarnessHostFactory
{
    private static readonly HarnessProfile[] HttpsProfiles =
    [
        HarnessProfile.BearerHttps,
        HarnessProfile.Cookie,
        HarnessProfile.CookieWithoutCsrfMiddleware,
        HarnessProfile.External,
    ];

    public static async Task<HarnessHostResult> StartAsync(HarnessProfile profile, HarnessHostOptions options, CancellationToken cancellationToken)
    {
        var description = profile.Describe();

        if (profile == HarnessProfile.External && string.IsNullOrWhiteSpace(options.ExternalAuthority))
            return HarnessHostResult.Unavailable(
                profile,
                description,
                "No external authority was supplied at host-construction time; the fake identity provider is wired in step 2.");

        var useHttps = HttpsProfiles.Contains(profile);
        var environmentName = profile switch
        {
            HarnessProfile.DevelopmentLogin => Environments.Development,
            _ => Environments.Production,
        };

        var databasePath = Path.Combine(options.TempDirectory, options.DatabaseFileName ?? $"{profile}.db");
        var certificate = useHttps ? options.Certificate : null;
        var captureProvider = new GuardExceptionCapture();

        WebApplication app;

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                EnvironmentName = environmentName,
                ApplicationName = "BlazorBase.User.SecurityHarness",
            });

            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(captureProvider);
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listenOptions =>
            {
                if (certificate is not null)
                    listenOptions.UseHttps(certificate);
            }));

            builder.Services.AddSingleton(options.Certificate);

            var configurationValues = new Dictionary<string, string?>
            {
                ["AuthTokens:AccessTokenMinutes"] = "60",
                ["AuthTokens:RefreshTokenDays"] = "30",
            };

            if (profile is HarnessProfile.DevelopmentLogin or HarnessProfile.ProductionWithDevelopmentLogin)
                configurationValues["DevelopmentAuthentication:Enabled"] = "true";

            if (profile == HarnessProfile.External)
            {
                configurationValues["ExternalAuthSettings:Authority"] = options.ExternalAuthority;
                configurationValues["ExternalAuthSettings:Audiences:0"] = "blazorbase-security-harness";

                if (options.ExternalAllowedTenantId is not null)
                    configurationValues["ExternalAuthSettings:AllowedTenantIds:0"] = options.ExternalAllowedTenantId;
            }

            foreach (var (key, value) in options.ConfigurationOverrides)
                configurationValues[key] = value;

            builder.Configuration.AddInMemoryCollection(configurationValues);

            builder.Services.AddDbContext<HarnessDbContext>(dbOptions => dbOptions.UseSqlite($"Data Source={databasePath}"));
            builder.Services.AddBlazorBaseUserServer<HarnessUser, HarnessDbContext>(builder.Configuration);
            builder.Services.AddSingleton<TimeProvider>(new HarnessTimeProvider());
            builder.Services.AddControllers();

            ConfigureLockoutDuration(builder);
            ConfigureDataProtectionTokenLifespan(builder);

            switch (profile)
            {
                case HarnessProfile.Cookie:
                case HarnessProfile.CookieWithoutCsrfMiddleware:
                    builder.Services.AddBlazorBaseCookieAuthTokens(builder.Configuration);
                    break;
                case HarnessProfile.AccessTokens:
                    builder.Services.AddBlazorBaseAccessTokens<HarnessDbContext>(builder.Configuration);
                    break;
                case HarnessProfile.External:
                    RegisterExternalAuth(builder, options);
                    break;
                case HarnessProfile.DevelopmentLogin:
                case HarnessProfile.ProductionWithDevelopmentLogin:
                    RegisterDevelopmentAuthentication(builder);
                    break;
            }

            app = builder.Build();
        }
        catch (Exception ex)
        {
            return HarnessHostResult.FailedToStart(profile, description, ex.Message);
        }

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();

        if (profile == HarnessProfile.Cookie)
            app.UseBlazorBaseCsrf();

        app.UseAuthorization();
        app.MapControllers();
        app.MapBlazorBaseAuthSessionEndpoint();
        app.MapBlazorBaseAccountEndpoints<HarnessUser>();

        MapHarnessProbeEndpoint(app);

        if (profile == HarnessProfile.External)
        {
            app.MapBlazorBaseExternalAuthEndpoints<HarnessUser>();
            app.MapBlazorBaseExternalAuthRedirectEndpoints<HarnessUser>(OpenIdConnectDefaults.AuthenticationScheme);
        }

        await EnsureDatabaseCreatedAsync(app, cancellationToken);

        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await app.DisposeAsync();
            return HarnessHostResult.FailedToStart(profile, description, ex.Message);
        }

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault();

        if (address is null)
        {
            await app.StopAsync(cancellationToken);
            await app.DisposeAsync();
            return HarnessHostResult.FailedToStart(profile, description, "Kestrel reported no listening address.");
        }

        var baseAddress = new Uri(address);

        var guardFailure = await ProbeStartupGuardAsync(baseAddress, certificate, captureProvider, cancellationToken);

        if (guardFailure is not null)
        {
            await app.StopAsync(cancellationToken);
            await app.DisposeAsync();
            return HarnessHostResult.FailedToStart(profile, description, guardFailure);
        }

        return HarnessHostResult.Started(profile, description, baseAddress, app, () => CreateProbeHttpClient(certificate));
    }

    /// <summary>
    /// Makes ASP.NET Core Identity's lockout duration configurable through
    /// <c>AuthTokens:LockoutSeconds</c>. <c>AddBlazorBaseUserServer</c> configures only the password
    /// policy, and Identity's lockout window has no configuration seam of its own, so
    /// <c>LOGIN-LOCKOUT-RESETS</c> would otherwise have to sit out a real five-minute wait.
    /// </summary>
    private static void ConfigureLockoutDuration(WebApplicationBuilder builder)
    {
        var lockoutSeconds = builder.Configuration.GetValue("AuthTokens:LockoutSeconds", 300);
        var maxFailedAccessAttempts = builder.Configuration.GetValue("AuthTokens:MaxFailedAccessAttempts", 5);
        builder.Services.Configure<IdentityOptions>(identityOptions =>
        {
            identityOptions.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromSeconds(lockoutSeconds);
            identityOptions.Lockout.MaxFailedAccessAttempts = maxFailedAccessAttempts;
        });
    }

    /// <summary>
    /// Binds <see cref="DataProtectionTokenProviderOptions.TokenLifespan"/> from
    /// <c>Tokens:DataProtection:TokenLifespan</c> - the framework itself never wires this, so an
    /// account-management probe that needs to observe a reset or confirmation token actually expire
    /// (rather than wait out Identity's one-day default) has no other seam to shorten it through.
    /// Left unbound in every other configuration, this changes nothing for any other profile.
    /// </summary>
    private static void ConfigureDataProtectionTokenLifespan(WebApplicationBuilder builder) =>
        builder.Services.Configure<DataProtectionTokenProviderOptions>(builder.Configuration.GetSection("Tokens:DataProtection"));

    /// <summary>
    /// Maps the harness's own minimal authenticated endpoint. It is not a framework route: a probe
    /// needs somewhere to observe the plain 401/200 distinction for a bearer token without depending
    /// on a protected route that only a particular host would define.
    /// </summary>
    private static void MapHarnessProbeEndpoint(WebApplication app)
    {
        app.MapGet("harness/protected-probe", () => Results.Ok()).RequireAuthorization();

        app.MapGet("harness/whoami", (ClaimsPrincipal user) => Results.Ok(new HarnessWhoAmIResponse(
                user.Identity?.Name,
                user.FindFirst(ClaimTypes.Email)?.Value)))
            .RequireAuthorization();

        app.MapGet("harness/access-token-probe", () => Results.Ok())
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AccessTokenAuthenticationDefaults.AuthenticationScheme });

        MapHarnessWriteProbeEndpoints(app);
    }

    /// <summary>
    /// Maps two application-shaped write endpoints for the cookie and CSRF probes. Neither carries
    /// <c>AllowBlazorBaseCsrf</c>, so <c>BlazorBaseCsrfMiddleware</c> is the only thing between a
    /// cross-site caller and either one.
    /// </summary>
    /// <remarks>
    /// The anonymous one stands in for a state-changing endpoint any caller can reach; the
    /// authenticated one stands in for the far more common shape, a write behind
    /// <c>RequireAuthorization()</c>. Having both lets a probe tell whether bypassing the CSRF check
    /// also carries the caller's identity, or only reaches something that never needed a session.
    /// </remarks>
    private static void MapHarnessWriteProbeEndpoints(WebApplication app)
    {
        app.MapPost("harness/guarded-probe", () => Results.Ok());
        app.MapPost("harness/protected-guarded-probe", () => Results.Ok()).RequireAuthorization();
    }

    /// <summary>
    /// Registers development authentication for a profile. Throws synchronously, inside this call,
    /// when <c>DevelopmentAuthentication:Enabled</c> is true outside the Development environment - this
    /// is where the startup guard for <see cref="HarnessProfile.ProductionWithDevelopmentLogin"/> fires.
    /// </summary>
    private static void RegisterDevelopmentAuthentication(WebApplicationBuilder builder) =>
        builder.Services.AddBlazorBaseDevelopmentAuthentication<HarnessUser>(builder.Configuration, builder.Environment);

    /// <summary>
    /// Registers a <see cref="BaseConfigurationManager"/> built over a thumbprint-pinning
    /// <see cref="HttpClient"/> before calling <c>AddBlazorBaseExternalAuth</c>, so
    /// <c>OidcExternalTokenValidator</c>'s constructor - which accepts one as an optional parameter -
    /// resolves it from DI instead of building its own unpinned <c>ConfigurationManager</c>. Every
    /// other part of the validator (signature verification, issuer and audience validation, <c>tid</c>
    /// handling) still runs unmodified; only its transport is pinned.
    /// </summary>
    private static void RegisterExternalAuth(WebApplicationBuilder builder, HarnessHostOptions options)
    {
        var configurationManager = PinnedThumbprintConfigurationManagerFactory.Create(options.ExternalAuthority!, options.Certificate);
        builder.Services.AddSingleton(configurationManager);

        if (options.IdentityProviderHost is not null)
            builder.Services.AddSingleton(options.IdentityProviderHost);

        builder.Services.AddBlazorBaseExternalAuth(builder.Configuration);

        builder.Services.AddSingleton<ExternalUserResolverSwitch>();
        builder.Services.AddScoped<StrictSubjectExternalUserResolver>();
        builder.Services.AddScoped<NaiveEmailMatchingExternalUserResolver>();
        builder.Services.AddScoped<IExternalUserResolver<HarnessUser>, SwitchingExternalUserResolver>();

        RegisterRedirectFlowAuthentication(builder, options);
    }

    /// <summary>
    /// Registers an actual <c>AddCookie</c>/<c>AddOpenIdConnect</c> pair, additively, so the
    /// browser-redirect half of external authentication
    /// (<c>MapBlazorBaseExternalAuthRedirectEndpoints</c>) runs against a real remote handler
    /// instead of a stub - the handler drives the fake provider's authorization and token endpoints
    /// exactly the way a real Entra ID app registration would drive Entra's. Pointed at the same
    /// <see cref="HarnessHostOptions.ExternalAuthority"/> the token-exchange flow uses, but under
    /// its own client id: the two flows validate independently (the OIDC handler's own
    /// <c>TokenValidationParameters</c> versus <c>OidcExternalTokenValidator</c>), and sharing a
    /// client id would not by itself couple them.
    /// </summary>
    private static void RegisterRedirectFlowAuthentication(WebApplicationBuilder builder, HarnessHostOptions options)
    {
        builder.Services.AddAuthentication()
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, cookieOptions =>
            {
                cookieOptions.Cookie.Name = "harness.external.redirect";
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, oidcOptions =>
            {
                oidcOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                oidcOptions.Authority = options.ExternalAuthority;
                oidcOptions.ClientId = RedirectFlowClientId;
                oidcOptions.ResponseType = OpenIdConnectResponseType.Code;
                oidcOptions.ResponseMode = OpenIdConnectResponseMode.Query;
                oidcOptions.UsePkce = false;
                oidcOptions.SaveTokens = false;
                oidcOptions.GetClaimsFromUserInfoEndpoint = false;
                oidcOptions.Backchannel = CreatePinnedBackchannel(options.Certificate);
            });
    }

    /// <summary>
    /// The client id the redirect flow's <c>AddOpenIdConnect</c> handler presents to the fake
    /// provider - independent of <c>ExternalAuthSettings:Audiences</c>, which only the separate
    /// token-exchange flow reads.
    /// </summary>
    private const string RedirectFlowClientId = "blazorbase-security-harness-redirect";

    /// <summary>
    /// An <see cref="HttpClient"/> that trusts exactly <paramref name="certificate"/>'s thumbprint -
    /// the same pinning discipline <see cref="PinnedThumbprintConfigurationManagerFactory"/> applies
    /// for the token-exchange flow, applied here to the OIDC handler's own metadata and
    /// authorization-code-exchange backchannel.
    /// </summary>
    private static HttpClient CreatePinnedBackchannel(X509Certificate2 certificate)
    {
        var pinnedThumbprint = certificate.Thumbprint;
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, presentedCertificate, _, _) =>
                presentedCertificate is not null && string.Equals(presentedCertificate.Thumbprint, pinnedThumbprint, StringComparison.OrdinalIgnoreCase),
        };

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// Creates the database schema before <c>app.StartAsync()</c> is called, not after:
    /// <c>AddBlazorBaseUserServer</c> registers <c>RefreshTokenCleanupService</c> as a hosted service,
    /// which starts running - and querying the database - the moment the host starts, before a
    /// post-start scope would ever get a chance to create the schema.
    /// </summary>
    private static async Task EnsureDatabaseCreatedAsync(WebApplication app, CancellationToken cancellationToken)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HarnessDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>
    /// Sends one harmless request to a freshly started host to surface the two startup guards
    /// (<c>BlazorBaseCsrfStartupGuardFilter</c>, <c>BlazorBaseAuthSessionEndpointStartupGuardFilter</c>),
    /// which are per-request middleware rather than a check that runs during <c>host.StartAsync()</c>: a
    /// host that registers the cookie transport without calling <c>app.UseBlazorBaseCsrf()</c> therefore
    /// starts cleanly and only fails once a request actually reaches it. This single harmless probe
    /// request surfaces that failure here instead of leaving it to be discovered by the first real
    /// probe. A caught <see cref="HttpRequestException"/> is exactly what a reset or aborted connection
    /// from an unhandled exception on that first request looks like from the outside;
    /// <paramref name="captureProvider"/> is the source of truth for whether that is what actually
    /// happened, so the exception itself is otherwise ignored.
    /// </summary>
    private static async Task<string?> ProbeStartupGuardAsync(Uri baseAddress, X509Certificate2? certificate, GuardExceptionCapture captureProvider, CancellationToken cancellationToken)
    {
        using var client = CreateProbeHttpClient(certificate);

        try
        {
            await client.GetAsync(baseAddress, cancellationToken);
        }
        catch (HttpRequestException)
        {
        }

        return captureProvider.FirstCapturedMessage;
    }

    /// <summary>
    /// Builds a probe client that never follows a redirect automatically - a probe must see the
    /// redirect itself to assert on it - and, for an HTTPS profile, trusts exactly the harness's own
    /// certificate thumbprint rather than bypassing validation altogether.
    /// </summary>
    private static HttpClient CreateProbeHttpClient(X509Certificate2? certificate)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };

        if (certificate is not null)
        {
            var pinnedThumbprint = certificate.Thumbprint;
            handler.ServerCertificateCustomValidationCallback = (_, presentedCertificate, _, _) =>
                presentedCertificate is not null &&
                string.Equals(presentedCertificate.Thumbprint, pinnedThumbprint, StringComparison.OrdinalIgnoreCase);
        }

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }
}

/// <summary>
/// The authenticated caller's own identity, as reported by <c>harness/whoami</c> - the
/// external-exchange account-linking probes need this to confirm which local user's tokens an
/// exchange actually minted, since the exchange endpoint's own response carries only the token
/// pair.
/// </summary>
public sealed record HarnessWhoAmIResponse(string? Name, string? Email);
