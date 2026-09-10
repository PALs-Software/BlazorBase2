using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.ExternalAuth;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.Tokens;

/// <summary>
/// End-to-end coverage for the Identity cookie (backend-for-frontend) token transport: the token pair
/// leaves as an <c>HttpOnly</c> session cookie and not in the body, the session cookie authenticates a
/// later request, a presented bearer header still wins so a native client is unaffected, the CSRF
/// filter refuses a state-changing request that cannot prove it came from the application itself, the
/// session survives past the old 15-minute access-token lifetime through sliding expiration, a
/// rotated security stamp invalidates an existing session, and logout revokes the session server-side
/// without affecting a second, independent session of the same user.
/// </summary>
public sealed class CookieAuthTokenTransportTests
{
    private const string SessionCookie = "bb_session";
    private const string CsrfHeader    = "X-CSRF-TOKEN";

    /// <summary>
    /// A <see cref="TimeProvider"/> a test advances by hand, so the two time-dependent findings this
    /// transport closes can be proven without a real wall-clock wait.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }

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

    private static async Task<(IHost Host, StubExternalTokenValidator Validator)> StartHostAsync(
        bool useCookies,
        TimeProvider? timeProvider = null,
        TimeSpan? securityStampValidationInterval = null,
        TimeSpan? cookieExpireTimeSpan = null,
        bool useCsrfMiddleware = true,
        bool useControllers = false,
        bool allowSameSiteRequests = false,
        bool claimDefaultScheme = true)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalAuthSettings:Authority"]      = "https://example-authority.test",
                ["ExternalAuthSettings:Audiences:0"]    = "example-audience",
                // The test server speaks plain HTTP, so a Secure cookie would never come back.
                ["AuthCookies:SecureOnly"]              = "false",
                ["AuthCookies:AllowSameSiteRequests"]   = allowSameSiteRequests ? "true" : "false",
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
                    services.AddBlazorBaseUserServer<TestUser, TestUserDbContext>(configuration, options =>
                    {
                        options.ClaimDefaultScheme = claimDefaultScheme;
                    });
                    services.AddBlazorBaseExternalAuth(configuration);

                    if (timeProvider is not null)
                        services.AddSingleton(timeProvider);

                    if (useCookies)
                    {
                        services.AddBlazorBaseCookieAuthTokens(configuration, configureOptions: options =>
                        {
                            if (cookieExpireTimeSpan is not null)
                                options.ExpireTimeSpan = cookieExpireTimeSpan.Value;
                        });
                    }

                    if (securityStampValidationInterval is not null)
                        services.PostConfigure<SecurityStampValidatorOptions>(o => o.ValidationInterval = securityStampValidationInterval.Value);

                    services.RemoveAll(typeof(IExternalTokenValidator));
                    services.AddSingleton<IExternalTokenValidator>(validator);
                    services.AddScoped<IExternalUserResolver<TestUser>, StubExternalUserResolver>();

                    if (useControllers)
                        services.AddControllers().AddApplicationPart(typeof(TestUserController).Assembly);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseBlazorBaseAccessTokenStampValidation();

                    if (useCookies && useCsrfMiddleware)
                        app.UseBlazorBaseCsrf();

                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        // The exchange is a credential-bearing login, not a CSRF target - no session
                        // exists yet for a cross-site page to ride along on.
                        endpoints.MapGroup(string.Empty).AllowBlazorBaseCsrf().MapBlazorBaseExternalAuthEndpoints<TestUser>();
                        endpoints.MapBlazorBaseAuthSessionEndpoint();
                        endpoints.MapGet("/protected", (HttpContext context) => context.User.Identity?.Name ?? "anonymous")
                                 .RequireAuthorization();
                        endpoints.MapPost("/guarded", () => Results.Ok("written"));
                        endpoints.MapPost("/allowed", () => Results.Ok("written"))
                                 .AllowBlazorBaseCsrf();
                        endpoints.MapPost("/logout", async (HttpContext context, IAuthTokenTransport transport) =>
                        {
                            await transport.ClearAsync(context);
                            return Results.NoContent();
                        });

                        if (useControllers)
                            endpoints.MapControllers();
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
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "known-subject"),
                    new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
                ],
                "External")));

        return (host, validator);
    }

    private static Task<HttpResponseMessage> ExchangeAsync(IHost host) =>
        ExchangeAsync(host, "valid-external-token");

    private static async Task<HttpResponseMessage> ExchangeAsync(IHost host, string externalToken) =>
        await host.GetTestClient().PostAsJsonAsync(
            "/api/auth/external/exchange",
            new ExternalTokenExchangeRequest { ExternalToken = externalToken });

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
    public async Task The_token_pair_leaves_as_a_session_cookie_and_not_in_the_body()
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
        Assert.True(cookies.ContainsKey(SessionCookie));
    }

    [Fact]
    public async Task The_session_cookie_is_marked_HttpOnly()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var response = await ExchangeAsync(host);
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();

        Assert.Contains(setCookies, c => c.StartsWith($"{SessionCookie}=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        // The antiforgery token travels in the session response body, so no readable cookie is set.
        Assert.DoesNotContain(setCookies, c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_session_cookie_authenticates_a_later_request()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var cookies = ReadCookies(await ExchangeAsync(host));

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Add("Cookie", $"{SessionCookie}={cookies[SessionCookie]}");

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
        request.Headers.Add("Cookie", $"{SessionCookie}={cookies[SessionCookie]}");
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

    /// <summary>
    /// Obtains an antiforgery token together with the cookie half it is bound to, exactly as a client
    /// does at startup.
    /// </summary>
    private static async Task<(string Token, string CookieHeader)> GetCsrfAsync(IHost host, string sessionCookieHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");

        if (!string.IsNullOrEmpty(sessionCookieHeader))
            request.Headers.Add("Cookie", sessionCookieHeader);

        var response = await host.GetTestClient().SendAsync(request);
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        var antiforgeryCookies = ReadCookies(response)
            .Select(entry => $"{entry.Key}={entry.Value}")
            .ToList();

        if (!string.IsNullOrEmpty(sessionCookieHeader))
            antiforgeryCookies.Add(sessionCookieHeader);

        return (session!.CsrfToken, string.Join("; ", antiforgeryCookies));
    }

    [Fact]
    public async Task A_state_changing_request_without_a_csrf_token_is_refused()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var (_, cookieHeader) = await GetCsrfAsync(host, string.Empty);

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Add("Cookie", cookieHeader);

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_state_changing_request_with_a_matching_csrf_token_passes()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var (token, cookieHeader) = await GetCsrfAsync(host, string.Empty);

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Add("Cookie", cookieHeader);
        request.Headers.Add(CsrfHeader, token);

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The token check alone would pass here. Fetch metadata is what closes the gap a sibling
    /// subdomain leaves open, since "same site" is registrable-domain scoped.
    /// </summary>
    [Fact]
    public async Task A_cross_site_request_is_refused_even_with_a_matching_csrf_token()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var (token, cookieHeader) = await GetCsrfAsync(host, string.Empty);

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Add("Cookie", cookieHeader);
        request.Headers.Add(CsrfHeader, token);
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// The default posture: a split-origin deployment (<c>app.</c> / <c>api.</c>) is same-site, not
    /// same-origin, and is refused unless the host explicitly opts in - see
    /// <see cref="AuthCookieOptions.AllowSameSiteRequests"/>.
    /// </summary>
    [Fact]
    public async Task A_same_site_request_is_refused_by_default()
    {
        var (host, _) = await StartHostAsync(useCookies: true, allowSameSiteRequests: false);
        using var _host = host;

        var (token, cookieHeader) = await GetCsrfAsync(host, string.Empty);

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Add("Cookie", cookieHeader);
        request.Headers.Add(CsrfHeader, token);
        request.Headers.Add("Sec-Fetch-Site", "same-site");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// With the split-origin deployment explicitly opted into, a same-site request with a valid
    /// antiforgery token passes - the token check is what still stands guard.
    /// </summary>
    [Fact]
    public async Task A_same_site_request_passes_when_explicitly_allowed()
    {
        var (host, _) = await StartHostAsync(useCookies: true, allowSameSiteRequests: true);
        using var _host = host;

        var (token, cookieHeader) = await GetCsrfAsync(host, string.Empty);

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Add("Cookie", cookieHeader);
        request.Headers.Add(CsrfHeader, token);
        request.Headers.Add("Sec-Fetch-Site", "same-site");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Reproduces the defect exactly: a host that sets <c>ClaimDefaultScheme = false</c> and
    /// registers no <c>DefaultForbidScheme</c> - the PlatformSubscriptions configuration - used to
    /// get an <see cref="InvalidOperationException"/> out of <c>Forbid()</c> instead of a response,
    /// because a scheme-less <c>Forbid()</c> has to ask the authentication middleware which scheme to
    /// challenge and there is no default to ask for. The controller now answers the status code
    /// directly instead of asking authentication to pick a scheme.
    /// </summary>
    /// <remarks>
    /// The request carries a bearer header so <see cref="BlazorBaseCsrfMiddleware"/> - which exempts
    /// any request carrying <c>Authorization</c> - lets it through to the controller. Without one, the
    /// middleware itself would answer 403 first and the controller's own
    /// <c>IsRequestIntentionalAsync</c> check - the one that used to call <c>Forbid()</c> - would
    /// never run. <see cref="IdentityCookieAuthTransport.IsRequestIntentionalAsync"/> does not carry
    /// that same exemption, so it still answers <see langword="false"/> for a request with no
    /// antiforgery proof, exactly reproducing the path that used to throw.
    /// </remarks>
    [Fact]
    public async Task Logout_without_proof_of_intent_answers_403_instead_of_throwing_when_no_forbid_scheme_is_configured()
    {
        var (host, _) = await StartHostAsync(
            useCookies: true,
            useControllers: true,
            claimDefaultScheme: false);
        using var _host = host;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new RefreshTokenRequest { RefreshToken = "irrelevant" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-token");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A token minted for a different caller must not work. This is what a bare random value in a
    /// readable cookie cannot give: anything able to write cookies could plant both halves of it.
    /// </summary>
    [Fact]
    public async Task A_csrf_token_from_another_session_is_refused()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var (_, ownCookies)    = await GetCsrfAsync(host, string.Empty);
        var (foreignToken, _)  = await GetCsrfAsync(host, string.Empty);

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Add("Cookie", ownCookies);
        request.Headers.Add(CsrfHeader, foreignToken);

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
    /// The client cannot decode an <c>HttpOnly</c> session cookie, so the session endpoint is its only
    /// way to learn its own claims. This walks the whole chain: cookie in, principal read from it,
    /// claims out.
    /// </summary>
    [Fact]
    public async Task The_session_endpoint_reports_the_claims_behind_the_cookie()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var cookies = ReadCookies(await ExchangeAsync(host));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("Cookie", $"{SessionCookie}={cookies[SessionCookie]}");

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
    }

    [Fact]
    public async Task Logout_clears_the_session_cookie()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var sessionCookie = ReadCookies(await ExchangeAsync(host))[SessionCookie];
        var (token, cookieHeader) = await GetCsrfAsync(host, $"{SessionCookie}={sessionCookie}");

        var request = new HttpRequestMessage(HttpMethod.Post, "/logout");
        request.Headers.Add("Cookie", cookieHeader);
        request.Headers.Add(CsrfHeader, token);

        var response = await host.GetTestClient().SendAsync(request);
        response.EnsureSuccessStatusCode();

        var clearedCookies = ReadCookies(response);
        Assert.True(clearedCookies.ContainsKey(SessionCookie));
        Assert.Empty(clearedCookies[SessionCookie]);

        var setCookieHeader = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
        Assert.Contains("expires=", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Closes <c>COOKIE-LOGOUT-CLEARS</c>: the pre-logout cookie value was cryptographically valid on
    /// its own, so replaying it after logout used to still authenticate even though the browser had
    /// been told to drop it. <see cref="BlazorBaseTicketStore"/> makes the cookie carry only a lookup
    /// key, and <c>ClearAsync</c>'s <c>SignOutAsync</c> call removes that key from the store, so the
    /// replayed value now resolves to nothing.
    /// </summary>
    [Fact]
    public async Task Logout_revokes_the_session_so_the_pre_logout_cookie_value_no_longer_authenticates()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var sessionCookie = ReadCookies(await ExchangeAsync(host))[SessionCookie];
        var (token, cookieHeader) = await GetCsrfAsync(host, $"{SessionCookie}={sessionCookie}");

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/logout");
        logoutRequest.Headers.Add("Cookie", cookieHeader);
        logoutRequest.Headers.Add(CsrfHeader, token);
        (await host.GetTestClient().SendAsync(logoutRequest)).EnsureSuccessStatusCode();

        var replayRequest = new HttpRequestMessage(HttpMethod.Get, "/protected");
        replayRequest.Headers.Add("Cookie", $"{SessionCookie}={sessionCookie}");

        var response = await host.GetTestClient().SendAsync(replayRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The property that separates server-side revocation from rotating the security stamp: signing
    /// out one session must not sign every other session of the same user out too. Two logins produce
    /// two independent <see cref="BlazorBaseTicketStore"/> keys, so removing the first one's key on
    /// logout leaves the second one's key, and therefore the second session, untouched.
    /// </summary>
    /// <remarks>
    /// The second login exchanges a distinct external token for a distinct token, rather than
    /// replaying <c>"valid-external-token"</c> — the exchange endpoint's own replay protection would
    /// otherwise reject the second exchange for an unrelated reason.
    /// </remarks>
    [Fact]
    public async Task A_second_sessions_cookie_survives_the_first_sessions_logout()
    {
        var (host, validator) = await StartHostAsync(useCookies: true);
        using var _host = host;

        validator.Register("second-session-token", new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "known-subject"),
                    new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
                ],
                "External")));

        var firstSessionCookie  = ReadCookies(await ExchangeAsync(host))[SessionCookie];
        var secondSessionCookie = ReadCookies(await ExchangeAsync(host, "second-session-token"))[SessionCookie];

        var (token, cookieHeader) = await GetCsrfAsync(host, $"{SessionCookie}={firstSessionCookie}");

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/logout");
        logoutRequest.Headers.Add("Cookie", cookieHeader);
        logoutRequest.Headers.Add(CsrfHeader, token);
        (await host.GetTestClient().SendAsync(logoutRequest)).EnsureSuccessStatusCode();

        var secondSessionRequest = new HttpRequestMessage(HttpMethod.Get, "/protected");
        secondSessionRequest.Headers.Add("Cookie", $"{SessionCookie}={secondSessionCookie}");

        var response = await host.GetTestClient().SendAsync(secondSessionRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The exact failure mode of finding #1: the old JWT-in-a-cookie transport had no refresh path, so
    /// every request 401'd once 15 minutes had passed since login, no matter how active the caller
    /// was. Sliding expiration renews the cookie on every request instead, so a caller who keeps
    /// visiting every 10 minutes is still signed in at the 20-minute mark that used to be unreachable —
    /// driven by a controllable clock rather than a real wall-clock wait.
    /// </summary>
    [Fact]
    public async Task The_session_survives_past_the_old_15_minute_access_token_lifetime_through_sliding_expiration()
    {
        var clock = new ManualTimeProvider();

        var (host, _) = await StartHostAsync(useCookies: true, timeProvider: clock, cookieExpireTimeSpan: TimeSpan.FromMinutes(15));
        using var _host = host;

        var sessionCookie = ReadCookies(await ExchangeAsync(host))[SessionCookie];
        var client = host.GetTestClient();

        for (var minute = 0; minute < 3; minute++)
        {
            clock.Advance(TimeSpan.FromMinutes(10));

            var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
            request.Headers.Add("Cookie", $"{SessionCookie}={sessionCookie}");

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Sliding expiration only reissues the cookie once past half its lifetime; keep whichever
            // value is current so the next iteration authenticates with a cookie that is still fresh.
            if (ReadCookies(response).TryGetValue(SessionCookie, out var renewed))
                sessionCookie = renewed;
        }
    }

    /// <summary>
    /// The exact failure mode of finding #2: the old transport carried no security stamp, so
    /// deactivating a user or rotating a role had no effect until the token expired on its own.
    /// <see cref="SecurityStampValidatorOptions.ValidationInterval"/> at zero validates on every
    /// request, so the rotated stamp is caught on the very next call — deterministic, no wall-clock
    /// wait required.
    /// </summary>
    [Fact]
    public async Task Rotating_the_users_security_stamp_invalidates_an_existing_session()
    {
        var (host, _) = await StartHostAsync(useCookies: true, securityStampValidationInterval: TimeSpan.Zero);
        using var _host = host;

        var sessionCookie = ReadCookies(await ExchangeAsync(host))[SessionCookie];
        var client = host.GetTestClient();

        var beforeRotation = new HttpRequestMessage(HttpMethod.Get, "/protected");
        beforeRotation.Headers.Add("Cookie", $"{SessionCookie}={sessionCookie}");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(beforeRotation)).StatusCode);

        using (var scope = host.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
            var user = await userManager.FindByNameAsync("known-subject");
            Assert.NotNull(user);
            var rotation = await userManager.UpdateSecurityStampAsync(user!);
            Assert.True(rotation.Succeeded);
        }

        var afterRotation = new HttpRequestMessage(HttpMethod.Get, "/protected");
        afterRotation.Headers.Add("Cookie", $"{SessionCookie}={sessionCookie}");

        var response = await client.SendAsync(afterRotation);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// This is the exact hole closed by moving CSRF enforcement into a middleware:
    /// <c>UserControllerBase.UpdateSettings</c> is an MVC controller action, which an endpoint filter
    /// can never reach, so it stayed unprotected under the old per-endpoint opt-in.
    /// </summary>
    [Fact]
    public async Task Put_user_settings_without_a_csrf_token_is_refused()
    {
        var (host, _) = await StartHostAsync(useCookies: true, useControllers: true);
        using var _host = host;

        var sessionCookie = ReadCookies(await ExchangeAsync(host))[SessionCookie];

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/user/me/settings")
        {
            Content = JsonContent.Create(new UpdateUserSettingsRequest { ThemePreference = "Dark", Language = "de" }),
        };
        request.Headers.Add("Cookie", $"{SessionCookie}={sessionCookie}");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_user_settings_with_a_valid_csrf_token_succeeds()
    {
        var (host, _) = await StartHostAsync(useCookies: true, useControllers: true);
        using var _host = host;

        var sessionCookie = ReadCookies(await ExchangeAsync(host))[SessionCookie];
        var (token, cookieHeader) = await GetCsrfAsync(host, $"{SessionCookie}={sessionCookie}");

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/user/me/settings")
        {
            Content = JsonContent.Create(new UpdateUserSettingsRequest { ThemePreference = "Dark", Language = "de" }),
        };
        request.Headers.Add("Cookie", cookieHeader);
        request.Headers.Add(CsrfHeader, token);

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// A bearer header is attached deliberately by the caller, which is the proof of intent a cookie
    /// cannot give - so it bypasses the check outright rather than needing a token of its own.
    /// </summary>
    [Fact]
    public async Task A_request_carrying_a_bearer_header_bypasses_the_csrf_check()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var request = new HttpRequestMessage(HttpMethod.Post, "/guarded");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-token");

        var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_endpoint_marked_AllowBlazorBaseCsrf_is_not_checked()
    {
        var (host, _) = await StartHostAsync(useCookies: true);
        using var _host = host;

        var response = await host.GetTestClient().PostAsync("/allowed", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A security control that silently does nothing is worse than one that is absent: a host that
    /// registers the cookie transport but forgets <c>app.UseBlazorBaseCsrf()</c> must find out on the
    /// very first request rather than shipping every write forgeable cross-site.
    /// </summary>
    [Fact]
    public async Task A_host_that_registers_the_cookie_transport_and_forgets_the_middleware_fails_loudly()
    {
        var (host, _) = await StartHostAsync(useCookies: true, useCsrfMiddleware: false);
        using var _host = host;

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.GetTestClient().GetAsync("/protected"));
    }
}
