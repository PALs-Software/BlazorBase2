using System.Security.Claims;
using System.Text.Encodings.Web;
using BlazorBase.User.Server.AccessTokens.BruteForce;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Authenticates requests carrying an access token from the <c>Authorization: Bearer</c> header.
/// Applies a per-client brute-force delay before validation.
/// </summary>
/// <remarks>
/// A query-string fallback is deliberately NOT supported. Query-string secrets leak into server and
/// proxy access logs, browser history, and <c>Referer</c> headers - places nobody audits and
/// everybody forwards. Every realistic caller (REST, MCP, git over Basic auth) can send a header.
///
/// The scheme is registered additively, so it never displaces the JWT default that browser sessions
/// use; endpoints opt in individually via <c>RequireAuthorization</c> naming
/// <see cref="AccessTokenAuthenticationDefaults.AuthenticationScheme"/>.
/// </remarks>
public class AccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> optionsMonitor,
    ILoggerFactory loggerFactory,
    UrlEncoder urlEncoder,
    IServiceScopeFactory scopeFactory,
    IBruteForceDelayService bruteForceDelayService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(optionsMonitor, loggerFactory, urlEncoder)
{
    #region Injects
    private readonly IServiceScopeFactory ScopeFactory = scopeFactory;
    private readonly IBruteForceDelayService BruteForceDelayService = bruteForceDelayService;
    #endregion

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var rawToken = ExtractToken();

        if (rawToken is null)
            return AuthenticateResult.NoResult();

        var clientKey = ResolveClientKey();
        await BruteForceDelayService.GetDelayAsync(clientKey, Context.RequestAborted);

        ValidatedAccessToken? validated;
        await using (var scope = ScopeFactory.CreateAsyncScope())
        {
            var accessTokenService = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
            validated = await accessTokenService.ValidateAsync(rawToken, Context.RequestAborted);
        }

        if (validated is null)
        {
            BruteForceDelayService.RegisterFailure(clientKey);
            return AuthenticateResult.Fail("Invalid, expired or revoked access token.");
        }

        BruteForceDelayService.RegisterSuccess(clientKey);

        _ = FireAndForgetTouchAsync(validated.TokenId);

        var claims = new List<Claim>
        {
            new(AccessTokenClaimTypes.TokenId, validated.TokenId.ToString()),
            new(ClaimTypes.Name, validated.Name)
        };

        if (validated.UserId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, validated.UserId));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private string? ExtractToken()
    {
        var authorizationHeader = Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authorizationHeader))
            return null;

        if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authorizationHeader["Bearer ".Length..].Trim();

        return null;
    }

    /// <summary>
    /// Always the transport-level remote address, never a client-suppliable header - see the
    /// remarks on <see cref="BruteForceDelayService"/> for why that distinction is load-bearing.
    /// </summary>
    private string ResolveClientKey()
    {
        var remoteIp = Context.Connection.RemoteIpAddress;

        if (remoteIp is not null)
            return remoteIp.ToString();

        return "unknown";
    }

    private async Task FireAndForgetTouchAsync(Guid tokenId)
    {
        try
        {
            await using var scope = ScopeFactory.CreateAsyncScope();
            var accessTokenService = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
            await accessTokenService.TouchAsync(tokenId);
        }
        catch (Exception exception)
        {
            Logger.LogDebug(exception, "Updating LastUsedAt for access token {TokenId} failed.", tokenId);
        }
    }
}
