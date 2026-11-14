using System.Net.Http.Headers;
using BlazorBase.Mailing;
using BlazorBase.Mailing.Models;
using BlazorBase.User.Server.Invitations;
using BlazorBase.User.Server.Invitations.Endpoints;
using BlazorBase.User.Server.Security;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;
using MailingEmailSender = BlazorBase.Mailing.Services.IEmailSender;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// A real <see cref="TestServer"/> with Identity (bearer transport), the invitation endpoints and - unless
/// <c>withMailing</c> is off - <c>BlazorBase.Mailing</c> with inline dispatch into
/// <see cref="RecordingMailingEmailSender"/>, so a test sees the rendered invitation email.
/// </summary>
public sealed class InvitationTestHost : IAsyncDisposable
{
    public const string BasePath = "/api/auth/invitations";

    private readonly SqliteConnection Connection;

    public IHost Host { get; }

    public RecordingMailingEmailSender SentEmails { get; }

    private InvitationTestHost(IHost host, SqliteConnection connection, RecordingMailingEmailSender sentEmails)
    {
        Host = host;
        Connection = connection;
        SentEmails = sentEmails;
    }

    public static async Task<InvitationTestHost> StartAsync(bool withMailing = true)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var schemaContext = new TestUserDbContext(new DbContextOptionsBuilder<TestUserDbContext>().UseSqlite(connection).Options))
            schemaContext.Database.EnsureCreated();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthTokens:AccessTokenMinutes"] = "60",
                ["AuthTokens:RefreshTokenDays"] = "30",
                ["AuthTokens:RateLimit:PermitLimit"] = "1000",
                ["AccountManagement:ClientBaseUrl"] = "https://app.example.com",
                ["EmailBranding:BrandName"] = "TestBrand",
            })
            .Build();

        var sentEmails = new RecordingMailingEmailSender();

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options => options.RequiresAuthSessionEndpoint = false);
                    services.AddBlazorBaseUserInvitations<TestUser>();

                    if (!withMailing)
                        return;

                    services.AddBlazorBaseMailing(configuration, dispatchMode: EmailDispatchMode.Inline);
                    services.RemoveAll<MailingEmailSender>();
                    services.AddSingleton<MailingEmailSender>(sentEmails);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseBlazorBaseAccessTokenStampValidation();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapBlazorBaseUserInvitationEndpoints<TestUser>());
                });
            })
            .StartAsync();

        using (var scope = host.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            await roleManager.CreateAsync(new IdentityRole("User"));
        }

        return new InvitationTestHost(host, connection, sentEmails);
    }

    public HttpClient CreateClient() => Host.GetTestClient();

    /// <summary>A client that carries the bearer token of a freshly seeded account with <paramref name="role"/>.</summary>
    public async Task<HttpClient> CreateClientForRoleAsync(string role, string email = "admin@example.com")
    {
        using var scope = Host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = new TestUser { UserName = email, Email = email, DisplayName = "Admin Person" };
        var result = await userManager.CreateAsync(user, "Secret123456");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        await userManager.AddToRoleAsync(user, role);

        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>();
        var tokens = await tokenIssuer.IssueTokenPairAsync(await signInManager.CreateUserPrincipalAsync(user));

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    public TestUserDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TestUserDbContext>().UseSqlite(Connection).Options);

    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();
        await Connection.DisposeAsync();
    }
}
