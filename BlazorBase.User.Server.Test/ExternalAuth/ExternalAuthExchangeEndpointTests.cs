using System.Globalization;
using System.Net.Http.Json;
using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.ExternalAuth;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.ExternalAuth;

/// <summary>
/// End-to-end tests for the external-token exchange endpoint mapped by
/// <c>MapBlazorBaseExternalAuthEndpoints</c>: a known subject exchanges for a real access/refresh
/// token pair, an unknown subject is rejected, an unvalidatable external token is rejected, and a
/// registered <see cref="IClaimsAugmentor{TUser}"/>'s claims land on the issued access token.
/// </summary>
public sealed class ExternalAuthExchangeEndpointTests
{
    private sealed class StubExternalTokenValidator : IExternalTokenValidator
    {
        private readonly Dictionary<string, ClaimsPrincipal?> _tokens = [];

        public void Register(string token, ClaimsPrincipal principal) => _tokens[token] = principal;

        public Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(_tokens.GetValueOrDefault(token));
    }

    private sealed class StubExternalUserResolver(TestUserDbContext dbContext) : IExternalUserResolver<TestUser>
    {
        public async Task<ExternalUserResolution<TestUser>> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken)
        {
            var subject = externalPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var user = subject is null
                ? null
                : await dbContext.Users.SingleOrDefaultAsync(u => u.UserName == subject, cancellationToken);

            return user is null
                ? ExternalUserResolution<TestUser>.Reject("Unknown subject.")
                : ExternalUserResolution<TestUser>.Accept(user);
        }
    }

    private sealed class TenantClaimsAugmentor : IClaimsAugmentor<TestUser>
    {
        public Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(TestUser user, IList<string> roles) =>
            Task.FromResult<IEnumerable<Claim>>([new Claim("tenant", "tenant-42")]);
    }

    private static ClaimsPrincipal BuildExternalPrincipal(string subject) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, subject),
                new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            ],
            "External"));

    private static async Task<(IHost Host, StubExternalTokenValidator Validator)> StartHostAsync(IDictionary<string, string?>? additionalConfiguration = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configurationValues = new Dictionary<string, string?>
        {
            ["ExternalAuthSettings:Authority"] = "https://example-authority.test",
            ["ExternalAuthSettings:Audiences:0"] = "example-audience",
        };

        if (additionalConfiguration is not null)
            foreach (var (key, value) in additionalConfiguration)
                configurationValues[key] = value;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var validator = new StubExternalTokenValidator();

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options =>
                    {
                        // This host is a bearer-only API and never maps the session endpoint.
                        options.RequiresAuthSessionEndpoint = false;
                    });
                    services.AddBlazorBaseExternalAuth(configuration);

                    services.RemoveAll(typeof(IExternalTokenValidator));
                    services.AddSingleton<IExternalTokenValidator>(validator);

                    services.AddScoped<IExternalUserResolver<TestUser>, StubExternalUserResolver>();
                    services.AddClaimsAugmentor<TestUser, TenantClaimsAugmentor>();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapBlazorBaseExternalAuthEndpoints<TestUser>());
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        return (host, validator);
    }

    private static async Task SeedUserAsync(IHost host, string userName)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        var result = await userManager.CreateAsync(new TestUser { UserName = userName, Email = $"{userName}@example.test" });
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private static async Task SetUserActiveAsync(IHost host, string userName, bool isActive)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByNameAsync(userName);
        user!.IsActive = isActive;
        await userManager.UpdateAsync(user);
    }

    private static async Task LockOutAsync(IHost host, string userName)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByNameAsync(userName);
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddYears(1));
    }

    /// <summary>
    /// Unprotects an access token minted by <see cref="AuthTokenIssuer"/> so a test can assert on its
    /// claims without a JWT library - the token is an opaque, data-protected blob.
    /// </summary>
    private static ClaimsPrincipal UnprotectAccessToken(IHost host, string accessToken)
    {
        using var scope = host.Services.CreateScope();
        var bearerTokenOptions = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(IdentityConstants.BearerScheme);

        return bearerTokenOptions.BearerTokenProtector.Unprotect(accessToken)!.Principal;
    }

    [Fact]
    public async Task A_known_subject_exchanges_the_external_token_for_a_real_token_pair()
    {
        var (host, validator) = await StartHostAsync();
        using var _ = host;
        await SeedUserAsync(host, "known-subject");
        validator.Register("valid-external-token", BuildExternalPrincipal("known-subject"));

        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.AccessToken);
        Assert.NotEmpty(body.RefreshToken);
    }

    [Fact]
    public async Task An_unknown_subject_is_rejected()
    {
        var (host, validator) = await StartHostAsync();
        using var _ = host;
        validator.Register("valid-external-token", BuildExternalPrincipal("nobody-registered-here"));

        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_invalid_or_expired_external_token_is_rejected()
    {
        var (host, _) = await StartHostAsync();
        using var _ = host;
        await SeedUserAsync(host, "known-subject");

        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "never-registered-token" });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_registered_augmentors_claims_reach_the_issued_access_token()
    {
        var (host, validator) = await StartHostAsync();
        using var _ = host;
        await SeedUserAsync(host, "known-subject");
        validator.Register("valid-external-token", BuildExternalPrincipal("known-subject"));

        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        var principal = UnprotectAccessToken(host, body!.AccessToken);
        Assert.Equal("tenant-42", principal.Claims.Single(c => c.Type == "tenant").Value);
    }

    [Fact]
    public async Task A_deactivated_users_resolution_is_rejected()
    {
        var (host, validator) = await StartHostAsync();
        using var _ = host;
        await SeedUserAsync(host, "known-subject");
        await SetUserActiveAsync(host, "known-subject", isActive: false);
        validator.Register("valid-external-token", BuildExternalPrincipal("known-subject"));

        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_locked_out_users_resolution_is_rejected()
    {
        var (host, validator) = await StartHostAsync();
        using var _ = host;
        await SeedUserAsync(host, "known-subject");
        await LockOutAsync(host, "known-subject");
        validator.Register("valid-external-token", BuildExternalPrincipal("known-subject"));

        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_same_token_exchanged_twice_succeeds_once_and_is_rejected_the_second_time()
    {
        var (host, validator) = await StartHostAsync();
        using var _ = host;
        await SeedUserAsync(host, "known-subject");
        validator.Register("valid-external-token", BuildExternalPrincipal("known-subject"));

        using var client = host.GetTestClient();
        var first = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });
        var second = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });

        Assert.Equal(System.Net.HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Two_different_tokens_for_the_same_user_both_succeed()
    {
        var (host, validator) = await StartHostAsync();
        using var _ = host;
        await SeedUserAsync(host, "known-subject");
        validator.Register("first-valid-external-token", BuildExternalPrincipal("known-subject"));
        validator.Register("second-valid-external-token", BuildExternalPrincipal("known-subject"));

        using var client = host.GetTestClient();
        var first = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "first-valid-external-token" });
        var second = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "second-valid-external-token" });

        Assert.Equal(System.Net.HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task With_replay_protection_disabled_the_same_token_can_be_exchanged_twice()
    {
        var (host, validator) = await StartHostAsync(new Dictionary<string, string?> { ["ExternalAuthSettings:ReplayProtectionEnabled"] = "false" });
        using var _ = host;
        await SeedUserAsync(host, "known-subject");
        validator.Register("valid-external-token", BuildExternalPrincipal("known-subject"));

        using var client = host.GetTestClient();
        var first = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });
        var second = await client.PostAsJsonAsync("/api/auth/external/exchange", new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });

        Assert.Equal(System.Net.HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, second.StatusCode);
    }
}

