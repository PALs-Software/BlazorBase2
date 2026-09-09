using System.Net;
using System.Security.Cryptography.X509Certificates;
using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        var databasePath = Path.Combine(options.TempDirectory, $"{profile}.db");
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
            }

            foreach (var (key, value) in options.ConfigurationOverrides)
                configurationValues[key] = value;

            builder.Configuration.AddInMemoryCollection(configurationValues);

            builder.Services.AddDbContext<HarnessDbContext>(dbOptions => dbOptions.UseSqlite($"Data Source={databasePath}"));
            builder.Services.AddBlazorBaseUserServer<HarnessUser, HarnessDbContext>(builder.Configuration);
            builder.Services.AddSingleton<TimeProvider>(new HarnessTimeProvider());
            builder.Services.AddControllers();

            switch (profile)
            {
                case HarnessProfile.Cookie:
                case HarnessProfile.CookieWithoutCsrfMiddleware:
                    builder.Services.AddBlazorBaseCookieAuthTokens(builder.Configuration);
                    break;
                case HarnessProfile.AccessTokens:
                    builder.Services.AddBlazorBaseAccessTokens<HarnessDbContext>(builder.Configuration);
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
    /// Registers development authentication for a profile. Throws synchronously, inside this call,
    /// when <c>DevelopmentAuthentication:Enabled</c> is true outside the Development environment - this
    /// is where the startup guard for <see cref="HarnessProfile.ProductionWithDevelopmentLogin"/> fires.
    /// </summary>
    private static void RegisterDevelopmentAuthentication(WebApplicationBuilder builder) =>
        builder.Services.AddBlazorBaseDevelopmentAuthentication<HarnessUser>(builder.Configuration, builder.Environment);

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
