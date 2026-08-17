using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

public class BlazorBaseUserAuthStateProvider(ITokenStorage tokenStorage) : AuthenticationStateProvider
{
    #region Injects
    private readonly ITokenStorage TokenStorage = tokenStorage;
    #endregion

    private ClaimsPrincipal CurrentUser = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var tokens = await TokenStorage.GetTokensAsync();
        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var claims = ParseClaimsFromJwt(tokens.AccessToken);
        if (claims is null)
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        CurrentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        return new AuthenticationState(CurrentUser);
    }

    public async Task MarkUserAsAuthenticated(LoginResponse tokens)
    {
        await TokenStorage.SaveTokensAsync(tokens);

        var claims = ParseClaimsFromJwt(tokens.AccessToken);
        CurrentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CurrentUser)));
    }

    public async Task MarkUserAsLoggedOut()
    {
        await TokenStorage.ClearTokensAsync();
        CurrentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CurrentUser)));
    }

    private static List<Claim>? ParseClaimsFromJwt(string jwt)
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
