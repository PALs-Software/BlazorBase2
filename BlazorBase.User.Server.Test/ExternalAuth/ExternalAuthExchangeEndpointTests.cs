using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.ExternalAuth;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
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
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subject)], "External"));

    private static async Task<(IHost Host, StubExternalTokenValidator Validator)> StartHostAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "blazorbase-external-auth-test-signing-secret-0123456789",
                ["JwtSettings:Issuer"] = "BlazorBase.User.Server.Test",
                ["JwtSettings:Audience"] = "BlazorBase.User.Server.Test.Audience",
                ["ExternalAuthSettings:Authority"] = "https://example-authority.test",
                ["ExternalAuthSettings:Audiences:0"] = "example-audience",
            })
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
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration);
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

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body!.AccessToken);
        Assert.Equal("tenant-42", jwt.Claims.Single(c => c.Type == "tenant").Value);
    }
}
