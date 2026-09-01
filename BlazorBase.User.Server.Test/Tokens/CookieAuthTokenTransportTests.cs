using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.ExternalAuth;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.Tokens;

/// <summary>
/// End-to-end coverage for the cookie (backend-for-frontend) token transport: the token pair leaves
/// as <c>HttpOnly</c> cookies and not in the body, the access cookie authenticates a later request, a
/// presented bearer header still wins so a native client is unaffected, and the CSRF filter refuses a
/// state-changing request that cannot prove it came from the application itself.
/// </summary>
public sealed class CookieAuthTokenTransportTests
{
    private const string AccessCookie  = "bb_access";
    private const string RefreshCookie = "bb_refresh";
    private const string CsrfCookie    = "XSRF-TOKEN";
    private const string CsrfHeader    = "X-CSRF-TOKEN";

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

    private static async Task<(IHost Host, StubExternalTokenValidator Validator)> StartHostAsync(bool useCookies)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"]                  = "blazorbase-cookie-transport-test-signing-secret-0123456789",
                ["JwtSettings:Issuer"]                  = "BlazorBase.User.Server.Test",
                ["JwtSettings:Audience"]                = "BlazorBase.User.Server.Test.Audience",
                ["ExternalAuthSettings:Authority"]      = "https://example-authority.test",
                ["ExternalAuthSettings:Audiences:0"]    = "example-audience",
                // The test server speaks plain HTTP, so a Secure cookie would never come back.
                ["AuthCookies:SecureOnly"]              = "false",
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

                    if (useCookies)
                        services.AddBlazorBaseCookieAuthTokens(configuration);

                    services.RemoveAll(typeof(IExternalTokenValidator));
                    services.AddSingleton<IExternalTokenValidator>(validator);
                    services.AddScoped<IExternalUserResolver<TestUser>, StubExternalUserResolver>();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapBlazorBaseExternalAuthEndpoints<TestUser>();
                        endpoints.MapBlazorBaseAuthSessionEndpoint();
                        endpoints.MapGet("/protected", (HttpContext context) => context.User.Identity?.Name ?? "anonymous")
                                 .RequireAuthorization();
                        endpoints.MapPost("/guarded", () => Results.Ok("written"))
                                 .RequireBlazorBaseCsrf();
                    });
                });
            })
            .StartAsync();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var result = await userManager.CreateAsync(new TestUser { UserName = "known-subject", Email = "known-subject@example.test" });
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        validator.Register("valid-external-token", new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "known-subject")], "External")));

        return (host, validator);
    }

    private static async Task<HttpResponseMessage> ExchangeAsync(IHost host) =>
        await host.GetTestClient().PostAsJsonAsync(
            "/api/auth/external/exchange",
            new ExternalTokenExchangeRequest { ExternalToken = "valid-external-token" });

    private static Dictionary<string, string> ReadCookies(HttpResponseMessage response)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            return cookies;

        foreach (var setCookie in setCookies)
        {
            var firstSegment = setCookie.Split(';')[0];
            var separator = firstSegment.IndexOf('=');

            if (separator > 0)
                cookies[firstSegment[..separator]] = firstSegment[(separator + 1)..];
        }

        return cookies;
    }

    [Fact]
    public async Task The_token_pair_leaves_as_cookies_and_not_in_the_body()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var response = await ExchangeAsync(host);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.AccessToken);
        Assert.Empty(body.RefreshToken);
        Assert.NotEqual(default, body.ExpiresAt);

        var cookies = ReadCookies(response);
        Assert.True(cookies.ContainsKey(AccessCookie));
        Assert.True(cookies.ContainsKey(RefreshCookie));
        Assert.NotEmpty(cookies[AccessCookie]);
    }

    [Fact]
    public async Task The_session_cookies_are_marked_HttpOnly_and_the_csrf_cookie_is_not()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var response = await ExchangeAsync(host);
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();

        Assert.Contains(setCookies, c => c.StartsWith($"{AccessCookie}=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, c => c.StartsWith($"{RefreshCookie}=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        // Deliberately readable — the client has to echo it back in a header.
        Assert.Contains(setCookies, c => c.StartsWith($"{CsrfCookie}=", StringComparison.Ordinal) && !c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_access_cookie_authenticates_a_later_request()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var cookies = ReadCookies(await ExchangeAsync(host));

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Add("Cookie", $"{AccessCookie}={cookies[AccessCookie]}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A bearer client and a cookie client must be able to share one server, so a presented header is
    /// never overridden by whatever cookie happens to be lying around.
    /// </summary>
    [Fact]
    public async Task A_presented_bearer_header_wins_over_the_cookie()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var cookies = ReadCookies(await ExchangeAsync(host));

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Add("Cookie", $"{AccessCookie}={cookies[AccessCookie]}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Without_the_opt_in_the_tokens_stay_in_the_body_and_no_cookie_is_set()
    {
        var (host, _) = await StartHostAsync(useCookies: false);
        using var _host = host;

        var response = await ExchangeAsync(host);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);

        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task A_state_changing_request_without_a_csrf_token_is_refused()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var cookies = ReadCookies(await ExchangeAsync(host));

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Add("Cookie", $"{CsrfCookie}={cookies[CsrfCookie]}");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_state_changing_request_with_a_matching_csrf_token_passes()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var cookies = ReadCookies(await ExchangeAsync(host));

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Add("Cookie", $"{CsrfCookie}={cookies[CsrfCookie]}");
        request.Headers.Add(CsrfHeader, cookies[CsrfCookie]);

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_cross_site_request_is_refused_even_with_a_matching_csrf_token()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var cookies = ReadCookies(await ExchangeAsync(host));

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Add("Cookie", $"{CsrfCookie}={cookies[CsrfCookie]}");
        request.Headers.Add(CsrfHeader, cookies[CsrfCookie]);
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_safe_method_needs_no_csrf_token()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var response = await host.GetTestClient().GetAsync("/protected");

        // Unauthorized rather than Forbidden: the CSRF filter let it through, authentication did not.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The client cannot decode an <c>HttpOnly</c> token, so the session endpoint is its only way to
    /// learn its own claims. This walks the whole chain: cookie in, JWT read from it, claims out.
    /// </summary>
    [Fact]
    public async Task The_session_endpoint_reports_the_claims_behind_the_cookie()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var cookies = ReadCookies(await ExchangeAsync(host));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("Cookie", $"{AccessCookie}={cookies[AccessCookie]}");

        var response = await host.GetTestClient().SendAsync(request);
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        Assert.True(session.IsAuthenticated);
        Assert.Contains(session.Claims, claim => claim.Value == "known-subject@example.test");
    }

    [Fact]
    public async Task The_session_endpoint_answers_an_anonymous_caller_instead_of_challenging_it()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var response = await host.GetTestClient().GetAsync("/api/auth/session");
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        Assert.False(session.IsAuthenticated);
        Assert.Empty(session.Claims);
    }
}
