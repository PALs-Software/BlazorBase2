using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.OAuth;
using BlazorBase.User.Server.OAuth.ClientMetadataDocuments;
using BlazorBase.User.Server.OAuth.Endpoints;
using BlazorBase.User.Server.OAuth.Maintenance;
using BlazorBase.User.Server.Security;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// A real <see cref="TestServer"/> host with Identity, the access-token scheme (with owner roles) and the
/// OAuth authorization server over one in-memory SQLite database, plus a protected endpoint at
/// <see cref="ProtectedResourcePath"/> registered as OAuth protected resource and an unregistered one at
/// <see cref="UnregisteredProtectedPath"/>. Carries the client-side steps of the flow - registration,
/// authorize, consent decision, token redemption - so each test reads as the protocol it exercises.
/// </summary>
/// <remarks>
/// <see cref="OAuthCleanupService{TUser}"/> is registered as a plain singleton instead of a hosted
/// service: its sweep at startup would use the one shared SQLite connection concurrently with the test's
/// requests, which that connection does not support. Tests that need a sweep run it themselves.
/// </remarks>
public sealed class OAuthTestHost : IAsyncDisposable
{
    public const string ProtectedResourcePath = "/api/mcp";
    public const string UnregisteredProtectedPath = "/api/other";
    public const string Issuer = "http://localhost";
    public const string RedirectUri = "https://claude.ai/api/mcp/auth_callback";

    private readonly SqliteConnection Connection;

    public IHost Host { get; }

    public ManualTimeProvider TimeProvider { get; }

    /// <summary>Answers every client id metadata document fetch; no request leaves the test process.</summary>
    public StubMetadataDocumentHandler MetadataDocuments { get; }

    private OAuthTestHost(IHost host, SqliteConnection connection, ManualTimeProvider timeProvider, StubMetadataDocumentHandler metadataDocuments)
    {
        Host = host;
        Connection = connection;
        TimeProvider = timeProvider;
        MetadataDocuments = metadataDocuments;
    }

    /// <summary>
    /// Starts a host. <paramref name="oauthSettings"/> entries are added below the <c>OAuthServer</c>
    /// section; <paramref name="registerAccessTokens"/> leaves out <c>AddBlazorBaseAccessTokens</c> to
    /// exercise the startup guard; <paramref name="useCookies"/> adds the cookie transport and the CSRF
    /// middleware; <paramref name="acceptAccessTokensByDefault"/> makes the default authorization policy accept
    /// access tokens next to the session bearer, as a host that opens its other APIs to access tokens does.
    /// </summary>
    public static async Task<OAuthTestHost> StartAsync(
        IDictionary<string, string?>? oauthSettings = null,
        bool registerAccessTokens = true,
        bool useCookies = false,
        bool acceptAccessTokensByDefault = false)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var schemaContext = new TestUserDbContext(new DbContextOptionsBuilder<TestUserDbContext>().UseSqlite(connection).Options))
            schemaContext.Database.EnsureCreated();

        var configurationEntries = new Dictionary<string, string?>
        {
            ["AuthTokens:AccessTokenMinutes"] = "60",
            ["AuthTokens:RefreshTokenDays"] = "30",
            ["AuthTokens:RateLimit:PermitLimit"] = "1000",
            ["AccessTokenSettings:SecretPrefix"] = "test_",
            ["AccessTokenSettings:BruteForceBaseDelayMs"] = "0",
            ["AccessTokenSettings:BruteForceMaxDelayMs"] = "0",
            ["AuthCookies:SecureOnly"] = "false",
        };

        foreach (var setting in oauthSettings ?? new Dictionary<string, string?>())
            configurationEntries[$"OAuthServer:{setting.Key}"] = setting.Value;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationEntries).Build();
        var timeProvider = new ManualTimeProvider();
        var metadataDocuments = new StubMetadataDocumentHandler();

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddSingleton<TimeProvider>(timeProvider);
                    services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(connection));
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options => options.RequiresAuthSessionEndpoint = false);

                    if (useCookies)
                        services.AddBlazorBaseCookieAuthTokens(configuration);

                    if (registerAccessTokens)
                    {
                        services.AddBlazorBaseAccessTokens<TestUserDbContext>(configuration);
                        services.AddAccessTokenOwnerRoleClaims<TestUser>();
                    }

                    if (acceptAccessTokensByDefault)
                        services.AddAuthorization(authorizationOptions => authorizationOptions.DefaultPolicy = new AuthorizationPolicyBuilder(
                                IdentityConstants.BearerScheme,
                                AccessTokenAuthenticationDefaults.AuthenticationScheme)
                            .RequireAuthenticatedUser()
                            .Build());

                    services.AddBlazorBaseOAuthServer<TestUser, TestUserDbContext>(configuration);
                    services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OAuthCleanupService<TestUser>)));
                    services.AddSingleton<OAuthCleanupService<TestUser>>();
                    services.AddHttpClient(OAuthClientIdMetadataDocumentFetcher.HttpClientName)
                        .ConfigurePrimaryHttpMessageHandler(() => metadataDocuments);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseBlazorBaseAccessTokenStampValidation();

                    if (useCookies)
                        app.UseBlazorBaseCsrf();

                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapBlazorBaseAuthSessionEndpoint();
                        endpoints.MapBlazorBaseOAuthServer();
                        endpoints.MapBlazorBaseOAuthProtectedResource(ProtectedResourcePath);

                        endpoints.MapGet(ProtectedResourcePath, (HttpContext httpContext) => Results.Ok(httpContext.User.Identity?.Name))
                            .RequireAuthorization(policy => policy
                                .AddAuthenticationSchemes(AccessTokenAuthenticationDefaults.AuthenticationScheme)
                                .RequireAuthenticatedUser());

                        endpoints.MapGet(UnregisteredProtectedPath, () => Results.Ok())
                            .RequireAuthorization(policy => policy
                                .AddAuthenticationSchemes(AccessTokenAuthenticationDefaults.AuthenticationScheme)
                                .RequireAuthenticatedUser());
                    });
                });
            })
            .StartAsync();

        return new OAuthTestHost(host, connection, timeProvider, metadataDocuments);
    }

    public HttpClient CreateClient() => Host.GetTestClient();

    public TestUserDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TestUserDbContext>().UseSqlite(Connection).Options);

    public async Task<TestUser> SeedUserAsync(string email, string role = "User", bool isActive = true)
    {
        using var scope = Host.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = new TestUser { UserName = email, Email = email, DisplayName = email, IsActive = isActive };
        var result = await userManager.CreateAsync(user, "Secret123456");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    public async Task RemoveFromRoleAsync(TestUser user, string role)
    {
        using var scope = Host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var currentUser = await userManager.FindByIdAsync(user.Id);

        var result = await userManager.RemoveFromRoleAsync(currentUser!, role);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    /// <summary>Mints the Identity bearer token a signed-in browser session would carry.</summary>
    public async Task<string> IssueSessionTokenAsync(TestUser user)
    {
        using var scope = Host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<TestUser>>();

        var currentUser = await userManager.FindByIdAsync(user.Id);
        var principal = await signInManager.CreateUserPrincipalAsync(currentUser!);
        var tokens = await tokenIssuer.IssueTokenPairAsync(principal);

        return tokens.AccessToken;
    }

    public async Task<HttpResponseMessage> RegisterAsync(object metadata) =>
        await CreateClient().PostAsJsonAsync("/api/oauth/register", metadata);

    /// <summary>Registers a client and returns its id and, for a confidential client, its secret.</summary>
    public async Task<(string ClientId, string? ClientSecret)> RegisterClientAsync(
        string tokenEndpointAuthMethod = "none",
        string[]? redirectUris = null,
        string[]? grantTypes = null,
        string clientName = "Claude")
    {
        var response = await RegisterAsync(new Dictionary<string, object>
        {
            ["client_name"] = clientName,
            ["redirect_uris"] = redirectUris ?? [RedirectUri],
            ["grant_types"] = grantTypes ?? ["authorization_code", "refresh_token"],
            ["response_types"] = new[] { "code" },
            ["token_endpoint_auth_method"] = tokenEndpointAuthMethod,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        var clientSecret = root.TryGetProperty("client_secret", out var secretElement) ? secretElement.GetString() : null;
        return (root.GetProperty("client_id").GetString()!, clientSecret);
    }

    public static string CreateCodeVerifier() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string ComputeCodeChallenge(string codeVerifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    /// <summary>Sends <c>GET authorize</c> with the given query parameters (a null value is left out).</summary>
    public async Task<HttpResponseMessage> AuthorizeAsync(IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var query = string.Join('&', parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}"));

        return await CreateClient().GetAsync($"/api/oauth/authorize?{query}");
    }

    /// <summary>The standard query of a valid authorization request; tests override single entries.</summary>
    public static Dictionary<string, string?> AuthorizeParameters(string clientId, string codeChallenge, string? state = "xyz") => new()
    {
        ["response_type"] = "code",
        ["client_id"] = clientId,
        ["redirect_uri"] = RedirectUri,
        ["code_challenge"] = codeChallenge,
        ["code_challenge_method"] = "S256",
        ["state"] = state,
        ["scope"] = "mcp",
        ["resource"] = Issuer + ProtectedResourcePath,
    };

    /// <summary>Runs <c>authorize</c> and returns the protected <c>request</c> the consent page receives.</summary>
    public async Task<string> StartAuthorizationAsync(string clientId, string codeChallenge, string? state = "xyz")
    {
        var response = await AuthorizeAsync(AuthorizeParameters(clientId, codeChallenge, state));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith("/oauth/consent?request=", location);

        return Uri.UnescapeDataString(location["/oauth/consent?request=".Length..]);
    }

    public async Task<HttpResponseMessage> DecideAsync(string? sessionToken, string request, bool approved)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/oauth/authorization-requests/decision")
        {
            Content = JsonContent.Create(new OAuthAuthorizationDecisionRequest { Request = request, Approved = approved }),
        };

        if (sessionToken is not null)
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        return await CreateClient().SendAsync(message);
    }

    /// <summary>Approves <paramref name="request"/> as <paramref name="user"/> and returns the redirect URI carrying the code.</summary>
    public async Task<Uri> ApproveAsync(TestUser user, string request)
    {
        var response = await DecideAsync(await IssueSessionTokenAsync(user), request, approved: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var decision = await response.Content.ReadFromJsonAsync<OAuthAuthorizationDecisionResponse>();
        return new Uri(decision!.RedirectUri);
    }

    public static string? ReadQueryValue(Uri uri, string name) =>
        QueryHelpers.ParseQuery(uri.Query).TryGetValue(name, out var values) ? values.ToString() : null;

    /// <summary>Runs authorize and consent for an existing client and returns the code.</summary>
    public async Task<string> ObtainCodeAsync(TestUser user, string clientId, string codeVerifier)
    {
        var request = await StartAuthorizationAsync(clientId, ComputeCodeChallenge(codeVerifier));
        var redirect = await ApproveAsync(user, request);
        return ReadQueryValue(redirect, "code")!;
    }

    public async Task<HttpResponseMessage> PostTokenAsync(IEnumerable<KeyValuePair<string, string>> form, AuthenticationHeaderValue? authorization = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/oauth/token") { Content = new FormUrlEncodedContent(form) };

        if (authorization is not null)
            message.Headers.Authorization = authorization;

        return await CreateClient().SendAsync(message);
    }

    public async Task<HttpResponseMessage> RedeemCodeAsync(string clientId, string code, string codeVerifier, string redirectUri = RedirectUri) =>
        await PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
            ["resource"] = Issuer + ProtectedResourcePath,
        });

    public async Task<HttpResponseMessage> RefreshAsync(string clientId, string refreshToken) =>
        await PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken,
        });

    /// <summary>Runs the whole flow for a public client and returns the parsed token response.</summary>
    public async Task<OAuthTokenPair> ObtainTokensAsync(TestUser user, string clientId)
    {
        var codeVerifier = CreateCodeVerifier();
        var code = await ObtainCodeAsync(user, clientId, codeVerifier);
        var response = await RedeemCodeAsync(clientId, code, codeVerifier);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadTokenPairAsync(response);
    }

    public static async Task<OAuthTokenPair> ReadTokenPairAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var refreshToken = root.TryGetProperty("refresh_token", out var refreshElement) ? refreshElement.GetString() : null;

        return new OAuthTokenPair(root.GetProperty("access_token").GetString()!, refreshToken, root.GetProperty("scope").GetString()!, root.GetProperty("expires_in").GetInt64());
    }

    public static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetString();
    }

    /// <summary>Calls the protected endpoint with <paramref name="accessToken"/>.</summary>
    public async Task<HttpResponseMessage> CallProtectedResourceAsync(string? accessToken, string path = ProtectedResourcePath)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, path);

        if (accessToken is not null)
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await CreateClient().SendAsync(message);
    }

    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();
        await Connection.DisposeAsync();
    }
}
