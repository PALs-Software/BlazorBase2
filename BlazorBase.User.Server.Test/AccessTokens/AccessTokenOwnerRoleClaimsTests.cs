using System.Net;
using System.Net.Http.Headers;
using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Authorization;
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
using Xunit;

namespace BlazorBase.User.Server.Test.AccessTokens;

/// <summary>
/// With <see cref="BlazorBaseAccessTokenServiceCollectionExtensions.AddAccessTokenOwnerRoleClaims{TUser}"/>
/// a role-protected endpoint accepts an access token by its owner's current roles, read per request.
/// </summary>
public sealed class AccessTokenOwnerRoleClaimsTests : IAsyncLifetime
{
    private const string RoleName = "Admin";

    private readonly SqliteConnection Connection = new("DataSource=:memory:");
    private IHost Host = null!;
    private HttpClient Client = null!;

    public async Task InitializeAsync()
    {
        Connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AccessTokenSettings:SecretPrefix"] = "test_",
                ["AccessTokenSettings:BruteForceBaseDelayMs"] = "0",
                ["AccessTokenSettings:BruteForceMaxDelayMs"] = "0"
            })
            .Build();

        Host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthorization();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(Connection));
                    services.AddIdentityCore<TestUser>()
                        .AddRoles<IdentityRole>()
                        .AddEntityFrameworkStores<TestUserDbContext>();
                    services.AddBlazorBaseAccessTokens<TestUserDbContext>(configuration);
                    services.AddAccessTokenOwnerRoleClaims<TestUser>();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/admin-only", () => Results.Ok())
                            .RequireAuthorization(new AuthorizeAttribute
                            {
                                AuthenticationSchemes = AccessTokenAuthenticationDefaults.AuthenticationScheme,
                                Roles = RoleName
                            });
                    });
                });
            })
            .StartAsync();

        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TestUserDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new IdentityRole(RoleName));

        Client = Host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Host.StopAsync();
        Host.Dispose();
        await Connection.DisposeAsync();
    }

    private async Task<TestUser> CreateOwnerAsync(bool withRole)
    {
        using var scope = Host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var owner = new TestUser { UserName = $"{Guid.NewGuid():N}@example.test", Email = $"{Guid.NewGuid():N}@example.test" };
        await userManager.CreateAsync(owner);

        if (withRole)
            await userManager.AddToRoleAsync(owner, RoleName);

        return owner;
    }

    private async Task<string> CreateTokenAsync(string? ownerId)
    {
        using var scope = Host.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<IAccessTokenService>().CreateAsync("probe", expiresAt: null, ownerId);
        return created.Secret;
    }

    private async Task<HttpStatusCode> CallAdminEndpointAsync(string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin-only");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        using var response = await Client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task A_token_of_an_owner_in_the_role_passes_the_role_check()
    {
        var owner = await CreateOwnerAsync(withRole: true);

        Assert.Equal(HttpStatusCode.OK, await CallAdminEndpointAsync(await CreateTokenAsync(owner.Id)));
    }

    [Fact]
    public async Task Removing_the_role_limits_the_token_on_the_next_request()
    {
        var owner = await CreateOwnerAsync(withRole: true);
        var secret = await CreateTokenAsync(owner.Id);
        Assert.Equal(HttpStatusCode.OK, await CallAdminEndpointAsync(secret));

        using (var scope = Host.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
            await userManager.RemoveFromRoleAsync((await userManager.FindByIdAsync(owner.Id))!, RoleName);
        }

        Assert.Equal(HttpStatusCode.Forbidden, await CallAdminEndpointAsync(secret));
    }

    [Fact]
    public async Task A_token_without_an_owner_gets_no_roles()
    {
        Assert.Equal(HttpStatusCode.Forbidden, await CallAdminEndpointAsync(await CreateTokenAsync(ownerId: null)));
    }
}
