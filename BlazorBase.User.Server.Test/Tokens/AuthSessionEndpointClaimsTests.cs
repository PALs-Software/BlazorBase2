using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Security;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Test.Infrastructure;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Server.Test.Tokens;

/// <summary>
/// <c>GET api/auth/session</c> must echo everything a client legitimately needs and nothing it
/// does not: the security stamp <see cref="SignInManager{TUser}.CreateUserPrincipalAsync"/> puts on
/// the same principal that mints the access token is internal bookkeeping and must never reach the
/// client, while every other claim - including one added by a registered
/// <see cref="IClaimsAugmentor{TUser}"/> - must survive.
/// </summary>
public sealed class AuthSessionEndpointClaimsTests
{
    private sealed class TenantClaimsAugmentor : IClaimsAugmentor<TestUser>
    {
        public Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(TestUser user, IList<string> roles) =>
            Task.FromResult<IEnumerable<Claim>>([new Claim("tenant", "tenant-42")]);
    }

    private static async Task<IHost> StartHostAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder().Build();

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
                    services.AddClaimsAugmentor<TestUser, TenantClaimsAugmentor>();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseBlazorBaseAccessTokenStampValidation();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapBlazorBaseAuthSessionEndpoint());
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        return host;
    }

    private static async Task<(TestUser User, string AccessToken)> SignInAsync(IHost host, string userName)
    {
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>();

        var user = new TestUser { UserName = userName, Email = $"{userName}@example.test" };
        var creationResult = await userManager.CreateAsync(user, "Secret123456");
        Assert.True(creationResult.Succeeded, string.Join("; ", creationResult.Errors.Select(e => e.Description)));

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var tokens = await tokenIssuer.IssueTokenPairAsync(principal);

        return (user, tokens.AccessToken);
    }

    [Fact]
    public async Task The_security_stamp_claim_is_not_echoed_to_an_authenticated_caller()
    {
        using var host = await StartHostAsync();
        var (_, accessToken) = await SignInAsync(host, "stamp-filter");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await host.GetTestClient().SendAsync(request);
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        Assert.True(session.IsAuthenticated);

        var securityStampClaimType = host.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType;
        Assert.DoesNotContain(session.Claims, claim => claim.Type == securityStampClaimType);
    }

    [Fact]
    public async Task Every_other_claim_including_an_augmentor_claim_still_reaches_the_client()
    {
        using var host = await StartHostAsync();
        var (user, accessToken) = await SignInAsync(host, "claims-survive");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await host.GetTestClient().SendAsync(request);
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        Assert.Contains(session.Claims, claim => claim.Type == ClaimTypes.NameIdentifier && claim.Value == user.Id);
        Assert.Contains(session.Claims, claim => claim.Type == ClaimTypes.Email && claim.Value == user.Email);
        Assert.Contains(session.Claims, claim => claim.Type == "displayName");
        Assert.Contains(session.Claims, claim => claim.Type == "themePreference");
        Assert.Contains(session.Claims, claim => claim.Type == "language");
        Assert.Contains(session.Claims, claim => claim.Type == "tenant" && claim.Value == "tenant-42");
    }

    [Fact]
    public async Task An_unauthenticated_caller_still_gets_no_claims()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/auth/session");
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        Assert.False(session.IsAuthenticated);
        Assert.Empty(session.Claims);
    }
}
