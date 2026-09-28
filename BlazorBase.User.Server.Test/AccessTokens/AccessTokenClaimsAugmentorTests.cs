using System.Net.Http.Headers;
using System.Security.Claims;
using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.AccessTokens;

/// <summary>
/// A registered <see cref="IAccessTokenClaimsAugmentor"/> is invoked end-to-end through
/// <see cref="AccessTokenAuthenticationHandler"/> - not merely resolvable from DI - and its claims
/// land on the principal a protected endpoint sees.
/// </summary>
public sealed class AccessTokenClaimsAugmentorTests
{
    private sealed class TenantClaimsAugmentor : IAccessTokenClaimsAugmentor
    {
        public Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(ValidatedAccessToken validatedAccessToken) =>
            Task.FromResult<IEnumerable<Claim>>(
                validatedAccessToken.Entity is TestDerivedAccessToken derived
                    ? [new Claim("tenant", derived.Binding ?? string.Empty)]
                    : []);
    }

    [Fact]
    public async Task A_registered_augmentor_adds_its_claims_to_the_authenticated_principal()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AccessTokenSettings:SecretPrefix"] = "test_",
                ["AccessTokenSettings:BruteForceBaseDelayMs"] = "0",
                ["AccessTokenSettings:BruteForceMaxDelayMs"] = "0"
            })
            .Build();

        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthorization();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
                    services.AddBlazorBaseAccessTokens<TestUserDbContext>(configuration);
                    services.AddAccessTokenClaimsAugmentor<TenantClaimsAugmentor>();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/probe", (ClaimsPrincipal user) =>
                                Results.Text(user.FindFirst("tenant")?.Value ?? string.Empty))
                            .RequireAuthorization(new AuthorizeAttribute
                            {
                                AuthenticationSchemes = AccessTokenAuthenticationDefaults.AuthenticationScheme
                            });
                    });
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var accessTokenService = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
        var created = await accessTokenService.CreateAsync<TestDerivedAccessToken>(
            "probe", expiresAt: null, token => token.Binding = "tenant-42");

        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created.Secret);

        var response = await client.GetAsync("/probe");
        response.EnsureSuccessStatusCode();

        Assert.Equal("tenant-42", await response.Content.ReadAsStringAsync());
    }
}
