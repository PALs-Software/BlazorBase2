using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BlazorBase.User.Server.ExternalAuth;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.ExternalAuth;

/// <summary>
/// Tests for the redirect-based external sign-in flow's <c>complete</c> action: it must sign out
/// of the local sign-in scheme - never the OIDC scheme, which would trigger a provider-side global
/// sign-out at its <c>end_session_endpoint</c> - and must never let a return URL escape the
/// application.
/// </summary>
public sealed class ExternalAuthRedirectEndpointMapperTests
{
    private const string ExternalScheme = "TestExternal";
    private const string SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    private sealed class SignOutRecorder
    {
        public List<string> SignedOutSchemes { get; } = [];
        public ClaimsPrincipal? SignedInPrincipal { get; set; }
    }

    /// <summary>
    /// Stands in for both the OIDC handler and the cookie handler it hands off to. A real remote
    /// handler only produces a principal for <c>signInScheme</c> - the scheme it hands its ticket off
    /// to - and returns <see cref="AuthenticateResult.NoResult"/> for its own scheme outside of the
    /// callback request it is processing, so this stub mirrors that split rather than answering for
    /// <c>externalScheme</c>. It never talks to a provider, so any request to an
    /// <c>end_session_endpoint</c> would have to come from somewhere else in the pipeline, and it
    /// records every scheme it is asked to sign out of.
    /// </summary>
    private sealed class RecordingAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SignOutRecorder recorder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationSignOutHandler
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Scheme.Name == SignInScheme && recorder.SignedInPrincipal is not null)
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(recorder.SignedInPrincipal, Scheme.Name)));

            return Task.FromResult(AuthenticateResult.NoResult());
        }

        public Task SignOutAsync(AuthenticationProperties? properties)
        {
            recorder.SignedOutSchemes.Add(Scheme.Name);
            return Task.CompletedTask;
        }
    }

    private sealed class StubExternalUserResolver(bool accept, TestUserDbContext dbContext) : IExternalUserResolver<TestUser>
    {
        public async Task<ExternalUserResolution<TestUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken)
        {
            if (!accept)
                return ExternalUserResolution<TestUser>.Reject("Rejected for test.");

            var user = await dbContext.Users.SingleAsync(cancellationToken);
            return ExternalUserResolution<TestUser>.Accept(user);
        }
    }

    private static ClaimsPrincipal BuildExternalPrincipal(string subject) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subject)], ExternalScheme));

    private static async Task<(IHost Host, SignOutRecorder Recorder)> StartHostAsync(bool acceptResolution)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder().Build();
        var recorder = new SignOutRecorder { SignedInPrincipal = BuildExternalPrincipal("subject") };

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddSingleton(recorder);
                    services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options =>
                    {
                        options.RequiresAuthSessionEndpoint = false;
                    });

                    services.AddAuthentication()
                        .AddScheme<AuthenticationSchemeOptions, RecordingAuthenticationHandler>(ExternalScheme, null)
                        .AddScheme<AuthenticationSchemeOptions, RecordingAuthenticationHandler>(SignInScheme, null);

                    services.AddScoped<IExternalUserResolver<TestUser>>(sp =>
                        new StubExternalUserResolver(acceptResolution, sp.GetRequiredService<TestUserDbContext>()));
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapBlazorBaseExternalAuthRedirectEndpoints<TestUser>(ExternalScheme));
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        return (host, recorder);
    }

    private static async Task SeedUserAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        var result = await userManager.CreateAsync(new TestUser { UserName = "known-subject", Email = "known-subject@example.test" });
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private static async Task SetUserActiveAsync(IHost host, bool isActive)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByNameAsync("known-subject");
        user!.IsActive = isActive;
        await userManager.UpdateAsync(user);
    }

    private static async Task LockOutAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByNameAsync("known-subject");
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddYears(1));
    }

    [Fact]
    public async Task An_accepted_resolution_issues_through_the_transport_and_redirects_locally()
    {
        var (host, recorder) = await StartHostAsync(acceptResolution: true);
        using var _ = host;
        await SeedUserAsync(host);

        using var client = host.GetTestClient();
        var response = await client.GetAsync("/api/auth/external/complete?returnUrl=/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard", response.Headers.Location?.OriginalString);
        Assert.Contains(SignInScheme, recorder.SignedOutSchemes);
        Assert.DoesNotContain(ExternalScheme, recorder.SignedOutSchemes);
    }

    [Fact]
    public async Task A_rejected_resolution_answers_401_and_writes_no_redirect()
    {
        var (host, recorder) = await StartHostAsync(acceptResolution: false);
        using var _ = host;

        using var client = host.GetTestClient();
        var response = await client.GetAsync("/api/auth/external/complete?returnUrl=/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains(SignInScheme, recorder.SignedOutSchemes);
        Assert.DoesNotContain(ExternalScheme, recorder.SignedOutSchemes);
    }

    [Theory]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("https://evil.com")]
    [InlineData("/\t/evil.com")]
    [InlineData("/\u0007evil.com")]
    [InlineData(" //evil.com")]
    [InlineData("/dashboard ")]
    [InlineData("\t/dashboard")]
    [InlineData("/dashboard\n")]
    public async Task An_unsafe_returnUrl_falls_back_to_the_default(string returnUrl)
    {
        var (host, _) = await StartHostAsync(acceptResolution: true);
        using var _ = host;
        await SeedUserAsync(host);

        using var client = host.GetTestClient();
        var response = await client.GetAsync($"/api/auth/external/complete?returnUrl={Uri.EscapeDataString(returnUrl)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task A_local_returnUrl_is_honoured()
    {
        var (host, _) = await StartHostAsync(acceptResolution: true);
        using var _ = host;
        await SeedUserAsync(host);

        using var client = host.GetTestClient();
        var response = await client.GetAsync($"/api/auth/external/complete?returnUrl={Uri.EscapeDataString("/dashboard")}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard", response.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// <see cref="RecordingAuthenticationHandler"/> never contacts a network endpoint, so this asserts
    /// the production code path - <c>SignOutAsync(signInScheme)</c>, never
    /// <c>SignOutAsync(externalScheme)</c> - which is what would trigger a real OIDC handler's
    /// redirect to its <c>end_session_endpoint</c>.
    /// </summary>
    [Fact]
    public async Task No_request_is_made_to_the_providers_end_session_endpoint()
    {
        var (host, recorder) = await StartHostAsync(acceptResolution: true);
        using var _ = host;
        await SeedUserAsync(host);

        using var client = host.GetTestClient();
        await client.GetAsync("/api/auth/external/complete");

        Assert.DoesNotContain(ExternalScheme, recorder.SignedOutSchemes);
    }

    [Fact]
    public async Task A_deactivated_users_resolution_answers_401_and_signs_out_the_local_scheme()
    {
        var (host, recorder) = await StartHostAsync(acceptResolution: true);
        using var _ = host;
        await SeedUserAsync(host);
        await SetUserActiveAsync(host, isActive: false);

        using var client = host.GetTestClient();
        var response = await client.GetAsync("/api/auth/external/complete?returnUrl=/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(SignInScheme, recorder.SignedOutSchemes);
        Assert.DoesNotContain(ExternalScheme, recorder.SignedOutSchemes);
    }

    [Fact]
    public async Task A_locked_out_users_resolution_answers_401_and_signs_out_the_local_scheme()
    {
        var (host, recorder) = await StartHostAsync(acceptResolution: true);
        using var _ = host;
        await SeedUserAsync(host);
        await LockOutAsync(host);

        using var client = host.GetTestClient();
        var response = await client.GetAsync("/api/auth/external/complete?returnUrl=/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(SignInScheme, recorder.SignedOutSchemes);
        Assert.DoesNotContain(ExternalScheme, recorder.SignedOutSchemes);
    }
}
