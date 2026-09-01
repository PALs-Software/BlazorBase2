using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

/// <summary>
/// Derives the authentication state from the token the client holds.
/// </summary>
/// <remarks>
/// Claim resolution is a single overridable step so a transport where the client holds no readable
/// token can supply its own — see <c>CookieAuthStateProvider</c>. The type itself stays the one the
/// UI components depend on, because replacing it with a sibling class would leave every one of them
/// unresolvable in that mode.
/// </remarks>
public class BlazorBaseUserAuthStateProvider(ITokenStorage tokenStorage) : AuthenticationStateProvider
{
    #region Injects
    private readonly ITokenStorage TokenStorage = tokenStorage;
    #endregion

    protected ClaimsPrincipal CurrentUser = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        CurrentUser = await ResolvePrincipalAsync();
        return new AuthenticationState(CurrentUser);
    }

    /// <summary>Builds the principal for the current session. Override to read it from elsewhere.</summary>
    protected virtual async Task<ClaimsPrincipal> ResolvePrincipalAsync()
    {
        var tokens = await TokenStorage.GetTokensAsync();
        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
            return new ClaimsPrincipal(new ClaimsIdentity());

        var claims = ParseClaimsFromJwt(tokens.AccessToken);
        if (claims is null)
            return new ClaimsPrincipal(new ClaimsIdentity());

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
    }

    public virtual async Task MarkUserAsAuthenticated(LoginResponse tokens)
    {
        await TokenStorage.SaveTokensAsync(tokens);

        var claims = ParseClaimsFromJwt(tokens.AccessToken);
        CurrentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CurrentUser)));
    }

    public virtual async Task MarkUserAsLoggedOut()
    {
        await TokenStorage.ClearTokensAsync();
        CurrentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CurrentUser)));
    }

    protected void NotifyCurrentUserChanged() =>
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CurrentUser)));

    protected static List<Claim>? ParseClaimsFromJwt(string jwt)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);
            return token.Claims.ToList();
        }
        catch
        {
            return null;
        }
    }
}
