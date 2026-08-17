using System.Net;
using System.Net.Http.Headers;
using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

public class AuthTokenHandler(
    ITokenStorage tokenStorage,
    IAuthService authService,
    BlazorBaseUserAuthStateProvider authStateProvider) : DelegatingHandler
{
    #region Injects
    private readonly ITokenStorage TokenStorage = tokenStorage;
    private readonly IAuthService AuthService = authService;
    private readonly BlazorBaseUserAuthStateProvider AuthStateProvider = authStateProvider;
    #endregion

    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Lets one refresh at a time run for the whole app.
    /// </summary>
    /// <remarks>
    /// Static on purpose: a host registers this handler once per typed client, so an instance field
    /// would only serialise the requests of a single client while a page that loads from two of them
    /// still refreshed twice. Refresh tokens rotate and the server treats a second use of a rotated
    /// token as theft — it revokes the whole family, which signs the user out everywhere. Two
    /// parallel calls made with the same token were therefore enough to end the session.
    /// </remarks>
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokens = await TokenStorage.GetTokensAsync();

        if (tokens is not null && !string.IsNullOrEmpty(tokens.AccessToken))
        {
            if (IsAboutToExpire(tokens) && !IsAuthEndpoint(request.RequestUri))
                tokens = await RefreshOnceAsync(tokens, cancellationToken) ?? tokens;

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized
            || tokens is null
            || string.IsNullOrEmpty(tokens.RefreshToken)
            || IsAuthEndpoint(request.RequestUri))
            return response;

        var refreshed = await RefreshOnceAsync(tokens, cancellationToken);

        if (refreshed is null)
            return response;

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Renews the tokens, but only once even when several requests notice the expiry together.
    /// Returns the fresh tokens, or null when the session could not be renewed.
    /// </summary>
    private async Task<LoginResponse?> RefreshOnceAsync(LoginResponse tokens, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(tokens.RefreshToken))
            return null;

        await RefreshGate.WaitAsync(cancellationToken);

        try
        {
            var current = await TokenStorage.GetTokensAsync();

            // Somebody else renewed while this request waited for the gate — take their result.
            if (current is not null
                && !string.IsNullOrEmpty(current.AccessToken)
                && current.RefreshToken != tokens.RefreshToken
                && !IsAboutToExpire(current))
                return current;

            var refreshToken = current?.RefreshToken ?? tokens.RefreshToken;
            var renewed = await AuthService.RefreshAsync(refreshToken!);
            await AuthStateProvider.MarkUserAsAuthenticated(renewed);

            return renewed;
        }
        catch (HttpRequestException exception) when (IsSessionRejected(exception))
        {
            await AuthStateProvider.MarkUserAsLoggedOut();
            return null;
        }
        catch (HttpRequestException)
        {
            /*
              The request never reached a verdict — no network, DNS, a proxy in the way. The session
              is not known to be invalid, so it is kept: signing out on a dropped connection means a
              phone moving between wifi and mobile data loses its login for no reason. The caller
              gets the failure of the original request instead.
            */
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    /// <summary>
    /// Whether the server actually turned the refresh token down, as opposed to never answering.
    /// </summary>
    private static bool IsSessionRejected(HttpRequestException exception)
        => exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    private static bool IsAboutToExpire(LoginResponse tokens)
        => tokens.ExpiresAt - DateTime.UtcNow < RefreshThreshold;

    private static bool IsAuthEndpoint(Uri? uri)
    {
        if (uri is null)
            return false;

        var path = uri.AbsolutePath.TrimEnd('/');
        return path.EndsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/api/auth/status", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/api/auth/setup", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/api/auth/logout", StringComparison.OrdinalIgnoreCase);
    }
}
