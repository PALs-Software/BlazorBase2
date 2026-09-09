using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using BlazorBase.User.Server.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.SecurityHarness.Probes.CookieTransport;

/// <summary>
/// Shared plumbing every cookie-transport and CSRF probe uses: reading the session cookie's own
/// configured name and CSRF header back from the host instead of hardcoding a default the host could
/// have overridden, fetching the antiforgery token an anonymous caller needs before it can reach any
/// state-changing endpoint at all, and logging in with that token attached - which every one of these
/// probes needs, since <c>api/auth/login</c> carries no exemption of its own from
/// <see cref="BlazorBaseCsrfMiddleware"/>.
/// </summary>
internal static class CookieTransportSupport
{
    public static string GetCsrfHeaderName(ProbeContext context) =>
        context.Services.GetRequiredService<IOptionsMonitor<AuthCookieOptions>>().CurrentValue.CsrfHeaderName;

    public static string GetSessionCookieName(ProbeContext context) =>
        context.Services.GetRequiredService<IOptionsMonitor<AuthCookieOptions>>().CurrentValue.EffectiveCookieName;

    /// <summary>
    /// Fetches the antiforgery request token from the anonymous session endpoint together with the
    /// antiforgery cookie half it is bound to - the precondition every state-changing cookie-transport
    /// request needs, since <see cref="BlazorBaseCsrfMiddleware"/> rejects a request with neither an
    /// antiforgery pair nor an <c>Authorization</c> header, and login itself carries neither by
    /// default. <paramref name="client"/> must be the same client instance used for every later
    /// request in the probe, so its own cookie handling keeps the antiforgery cookie attached.
    /// </summary>
    public static async Task<(string CsrfToken, ProbeExchange Exchange)> FetchCsrfTokenAsync(HttpClient client, Uri baseAddress, CancellationToken cancellationToken)
    {
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, baseAddress, "api/auth/session", cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<AuthSessionResponse>(exchange.ResponseBody, out var session);

        return (session?.CsrfToken ?? string.Empty, exchange);
    }

    /// <summary>Logs in with the given antiforgery token attached under <paramref name="csrfHeaderName"/>.</summary>
    public static async Task<(LoginResponse? Tokens, ProbeExchange Exchange)> LoginWithCsrfAsync(HttpClient client, Uri baseAddress, string csrfHeaderName, string? csrfToken, string email, string password, CancellationToken cancellationToken)
    {
        var headers = csrfToken is null ? null : new[] { new KeyValuePair<string, string>(csrfHeaderName, csrfToken) };
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Post, baseAddress, "api/auth/login",
            new LoginRequest { Email = email, Password = password }, headers: headers, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<LoginResponse>(exchange.ResponseBody, out var tokens);

        return (exchange.StatusCode == HttpStatusCode.OK ? tokens : null, exchange);
    }

    /// <summary>
    /// Seeds a user, fetches an anonymous antiforgery token and logs in with it - the sequence every
    /// probe that needs a real cookie session starts from. <paramref name="client"/> carries the
    /// resulting session and antiforgery cookies for whatever the probe sends through it next.
    /// </summary>
    public static async Task<CookieSessionResult> EstablishSessionAsync(ProbeContext context, HttpClient client, string emailLocalPart, CancellationToken cancellationToken)
    {
        var email = CoreSessionSupport.UniqueEmail(emailLocalPart);
        await CoreSessionSupport.SeedUserAsync(context, email);

        var (csrfToken, sessionExchange) = await FetchCsrfTokenAsync(client, context.BaseAddress, cancellationToken);
        var (tokens, loginExchange) = await LoginWithCsrfAsync(client, context.BaseAddress, GetCsrfHeaderName(context), csrfToken, email, CoreSessionSupport.SeedPassword, cancellationToken);

        return new CookieSessionResult(email, csrfToken, tokens, sessionExchange, loginExchange);
    }

    /// <summary>Every raw <c>Set-Cookie</c> header line on <paramref name="exchange"/>'s response, verbatim.</summary>
    public static IReadOnlyList<string> GetSetCookieHeaders(ProbeExchange exchange) =>
        exchange.ResponseHeaders.TryGetValues("Set-Cookie", out var values) ? [.. values] : [];

    /// <summary>The one <c>Set-Cookie</c> header line whose cookie name matches <paramref name="cookieName"/>, if present.</summary>
    public static string? FindCookieHeader(IReadOnlyList<string> setCookieHeaders, string cookieName) =>
        setCookieHeaders.FirstOrDefault(header => header.StartsWith($"{cookieName}=", StringComparison.Ordinal));
}

/// <summary>The outcome of <see cref="CookieTransportSupport.EstablishSessionAsync"/>.</summary>
/// <param name="Email">The seeded user's email, in case a probe needs to log in again as the same account.</param>
/// <param name="CsrfToken">The antiforgery token fetched anonymously and used to log in.</param>
/// <param name="Tokens">The login response, or <see langword="null"/> when login did not answer 200.</param>
/// <param name="SessionExchange">The anonymous <c>GET api/auth/session</c> call that minted <see cref="CsrfToken"/>.</param>
/// <param name="LoginExchange">The <c>POST api/auth/login</c> call.</param>
internal sealed record CookieSessionResult(string Email, string CsrfToken, LoginResponse? Tokens, ProbeExchange SessionExchange, ProbeExchange LoginExchange);
