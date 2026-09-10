using System.Net;
using System.Security.Cryptography.X509Certificates;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.IdentityProvider;
using BlazorBase.User.Server.ExternalAuth;
using BlazorBase.User.Server.Security;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.SecurityHarness.Probes.ExternalExchange;

/// <summary>
/// Exercises <c>BlazorBase.User.Server</c>'s own default <c>IExternalUserResolver&lt;TUser&gt;</c> -
/// <c>IdentityLoginExternalUserResolver&lt;TUser&gt;</c> - directly, never the harness's own
/// <c>SwitchingExternalUserResolver</c>. The shared <see cref="HarnessProfile.External"/> host
/// registers <c>SwitchingExternalUserResolver</c> with a plain <c>AddScoped</c> after
/// <c>AddBlazorBaseUserServer</c>'s <c>TryAddScoped</c> default, so it always wins there regardless
/// of configuration - <see cref="ProbeContext.StartIsolatedHostAsync"/> can only start another
/// instance of that same shared host and therefore can never reach the framework default at all.
/// This probe instead starts its own separate host wiring only <c>AddBlazorBaseUserServer</c> and
/// <c>AddBlazorBaseExternalAuth</c>, borrowing the already-running fake identity provider and its
/// pinned certificate from <paramref name="context"/> - nothing there claims
/// <c>IExternalUserResolver&lt;HarnessUser&gt;</c>, so the framework's own default answers. Covers
/// both halves of what the default is for in one run: a subject linked through
/// <see cref="UserManager{TUser}.AddLoginAsync(TUser, UserLoginInfo)"/> is accepted, and the same
/// unverified-email-takeover shape <see cref="ExtUnverifiedEmailTakeoverProbe"/> demonstrates
/// against the harness's own naive resolver is instead rejected here - there is no other probe in
/// this profile exercising the framework default's own happy path.
/// </summary>
internal sealed class ExtDefaultResolverRejectsProbe : IAuthProbe
{
    public string Id => "EXT-DEFAULT-RESOLVER-REJECTS";
    public string Category => "External token exchange";
    public string Attack => "A subject linked via Identity's own login table, then a second token with an unrecognised subject whose email claim matches an existing local account - both run against the framework's own default resolver, never the harness's.";
    public string Expectation => "The linked subject is accepted; the unrecognised subject is rejected regardless of a matching email claim.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.High;
    public HarnessProfile Profile => HarnessProfile.External;

    public async Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var providerHost = context.Services.GetRequiredService<FakeIdentityProviderHost>();
        var certificate = context.Services.GetRequiredService<X509Certificate2>();
        var evidence = new List<ProbeEvidence>();

        await using var defaultResolverHost = await DefaultResolverHost.StartAsync(providerHost, certificate, cancellationToken);
        using var client = new HttpClient { BaseAddress = defaultResolverHost.BaseAddress };

        const string linkedSubject = "default-resolver-linked-subject";
        await defaultResolverHost.CreateLinkedUserAsync(linkedSubject);

        var acceptedDescription = ExternalExchangeSupport.CreateValidDescription(providerHost, subject: linkedSubject, audience: DefaultResolverHost.Audience);
        var acceptedToken = ExternalExchangeSupport.SignWithKeyA(providerHost, acceptedDescription);
        var acceptedExchange = await ExternalExchangeSupport.ExchangeAsync(client, defaultResolverHost.BaseAddress, acceptedToken, cancellationToken);
        evidence.Add(ExternalExchangeSupport.BuildEvidence("Framework default resolver: oid matches a subject linked via UserManager.AddLoginAsync.", acceptedToken, acceptedExchange));

        if (acceptedExchange.StatusCode != HttpStatusCode.OK)
            return ProbeResult.Failed($"The default resolver rejected a subject linked through Identity's own login table (expected 200, got {(int)acceptedExchange.StatusCode} {acceptedExchange.StatusCode}).", evidence);

        var victimEmail = $"default-resolver-victim-{Guid.NewGuid():N}@harness.local";
        await defaultResolverHost.CreateUnlinkedUserAsync(victimEmail);

        var takeoverDescription = ExternalExchangeSupport.CreateValidDescription(providerHost, subject: "default-resolver-attacker-subject-never-linked", audience: DefaultResolverHost.Audience, email: victimEmail, emailVerified: false);
        var takeoverToken = ExternalExchangeSupport.SignWithKeyA(providerHost, takeoverDescription);
        var takeoverExchange = await ExternalExchangeSupport.ExchangeAsync(client, defaultResolverHost.BaseAddress, takeoverToken, cancellationToken);
        evidence.Add(ExternalExchangeSupport.BuildEvidence($"Framework default resolver: email = '{victimEmail}' (an existing local account), subject never linked.", takeoverToken, takeoverExchange));

        if (takeoverExchange.StatusCode != HttpStatusCode.Unauthorized)
            return ProbeResult.Failed($"The default resolver accepted an unrecognised subject whose email matched an existing account (expected 401, got {(int)takeoverExchange.StatusCode} {takeoverExchange.StatusCode}).", evidence);

        return ProbeResult.Passed("The framework's default resolver accepted a subject linked via Identity's own login table and rejected an unrecognised subject regardless of a matching email claim.", evidence);
    }

    /// <summary>
    /// A private, minimal host wiring only <c>AddBlazorBaseUserServer</c> and
    /// <c>AddBlazorBaseExternalAuth</c> - deliberately not <see cref="Hosting.HarnessHostFactory"/>,
    /// which always additionally registers <c>SwitchingExternalUserResolver</c> for
    /// <see cref="HarnessProfile.External"/> and would therefore always shadow the resolver this
    /// probe exists to exercise. Serves over plain HTTP - the only HTTPS endpoint this probe ever
    /// reaches is the fake identity provider's own, already reached through the pinned
    /// <c>BaseConfigurationManager</c> registered below, never this host's own listener.
    /// </summary>
    private sealed class DefaultResolverHost : IAsyncDisposable
    {
        public const string Audience = "blazorbase-security-harness-default-resolver";

        private readonly WebApplication App;
        private readonly string DatabasePath;

        public Uri BaseAddress { get; }

        private DefaultResolverHost(WebApplication app, Uri baseAddress, string databasePath)
        {
            App = app;
            BaseAddress = baseAddress;
            DatabasePath = databasePath;
        }

        public static async Task<DefaultResolverHost> StartAsync(FakeIdentityProviderHost providerHost, X509Certificate2 certificate, CancellationToken cancellationToken)
        {
            var authority = providerHost.TenantAuthority(FakeIdentityProviderTenants.AllowedTenantId);
            var databasePath = Path.Combine(Path.GetTempPath(), $"ext-default-resolver-{Guid.NewGuid():N}.db");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                EnvironmentName = Environments.Production,
                ApplicationName = "BlazorBase.User.SecurityHarness.DefaultResolverProbe",
            });

            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalAuthSettings:Authority"] = authority,
                ["ExternalAuthSettings:Audiences:0"] = Audience,
                ["ExternalAuthSettings:AllowedTenantIds:0"] = FakeIdentityProviderTenants.AllowedTenantId,
            });

            builder.Services.AddDbContext<HarnessDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
            builder.Services.AddSingleton(PinnedThumbprintConfigurationManagerFactory.Create(authority, certificate));
            builder.Services.AddBlazorBaseUserServer<HarnessUser, HarnessDbContext>(builder.Configuration, options =>
                options.RequiresAuthSessionEndpoint = false);
            builder.Services.AddBlazorBaseExternalAuth(builder.Configuration);

            var app = builder.Build();

            app.UseRouting();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseBlazorBaseAccessTokenStampValidation();
            app.UseAuthorization();
            app.MapBlazorBaseExternalAuthEndpoints<HarnessUser>();

            using (var scope = app.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<HarnessDbContext>().Database.EnsureCreatedAsync(cancellationToken);

            await app.StartAsync(cancellationToken);

            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
                ?? throw new InvalidOperationException("The default-resolver probe host reported no listening address.");

            return new DefaultResolverHost(app, new Uri(address), databasePath);
        }

        public async Task CreateLinkedUserAsync(string subject)
        {
            using var scope = App.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HarnessUser>>();
            var user = new HarnessUser { UserName = subject, Email = $"{subject}@harness.local", DisplayName = subject, IsActive = true };

            var createResult = await users.CreateAsync(user);
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"Could not seed user '{subject}': {string.Join("; ", createResult.Errors.Select(error => error.Description))}");

            var linkResult = await users.AddLoginAsync(user, new UserLoginInfo(ExternalAuthSettings.DefaultLoginProvider, subject, subject));
            if (!linkResult.Succeeded)
                throw new InvalidOperationException($"Could not link subject '{subject}': {string.Join("; ", linkResult.Errors.Select(error => error.Description))}");
        }

        public async Task CreateUnlinkedUserAsync(string email)
        {
            using var scope = App.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HarnessUser>>();
            var user = new HarnessUser { UserName = email, Email = email, DisplayName = email, IsActive = true };

            var createResult = await users.CreateAsync(user);
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"Could not seed user '{email}': {string.Join("; ", createResult.Errors.Select(error => error.Description))}");
        }

        public async ValueTask DisposeAsync()
        {
            await App.StopAsync();
            await App.DisposeAsync();

            try
            {
                File.Delete(DatabasePath);
            }
            catch (IOException)
            {
            }
        }
    }
}
