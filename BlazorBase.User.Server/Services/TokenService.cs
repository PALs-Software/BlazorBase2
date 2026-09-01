using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Server.Services;

public class TokenService<TUser>(IConfiguration configuration, IUserClaimsPrincipalFactory<TUser> claimsPrincipalFactory) where TUser : BaseUser
{
    #region Injects
    private readonly IConfiguration Configuration = configuration;
    private readonly IUserClaimsPrincipalFactory<TUser> ClaimsPrincipalFactory = claimsPrincipalFactory;
    #endregion

    /// <param name="roles">
    /// Unused by this method: the claims principal factory reads the user's roles from the store
    /// itself. Kept so callers built for the previous claim-building code keep compiling unchanged.
    /// </param>
    public async Task<string> GenerateAccessToken(TUser user, IList<string> roles)
    {
        var secret = Configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JwtSettings:Secret is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var principal = await ClaimsPrincipalFactory.CreateAsync(user);
        var claims = principal.Claims.ToList();

        var expirationMinutes = Configuration.GetValue("JwtSettings:ExpirationMinutes", JwtSettingsDefaults.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: Configuration["JwtSettings:Issuer"],
            audience: Configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public string HashRefreshToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Issues an access/refresh token pair for <paramref name="user"/> and persists the refresh
    /// token, exactly as <c>AuthControllerBase.GenerateTokenResponse</c> does for the interactive
    /// login/setup/refresh endpoints - shared here so every caller that mints a framework token
    /// pair (the auth controller and the external-token exchange endpoint alike) goes through one
    /// implementation instead of two that could drift apart.
    /// </summary>
    /// <param name="familyId">
    /// The refresh-token family to continue, or <see langword="null"/>/<see cref="Guid.Empty"/> to
    /// start a new one - see <c>RefreshToken.FamilyId</c>.
    /// </param>
    public async Task<LoginResponse> IssueTokenPairAsync(
        TUser user,
        IList<string> roles,
        BaseUserDbContext<TUser> dbContext,
        Guid? familyId = null)
    {
        var accessToken = await GenerateAccessToken(user, roles);
        var refreshTokenValue = GenerateRefreshToken();

        var refreshExpirationDays = Configuration.GetValue("JwtSettings:RefreshExpirationDays", JwtSettingsDefaults.RefreshTokenExpirationDays);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = HashRefreshToken(refreshTokenValue),
            UserId = user.Id,
            FamilyId = familyId is null || familyId.Value == Guid.Empty ? Guid.NewGuid() : familyId.Value,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpirationDays),
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync();

        var expirationMinutes = Configuration.GetValue("JwtSettings:ExpirationMinutes", JwtSettingsDefaults.AccessTokenExpirationMinutes);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
        };
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var secret = Configuration["JwtSettings:Secret"];
        if (string.IsNullOrEmpty(secret))
            return null;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = Configuration["JwtSettings:Issuer"],
                ValidateAudience = true,
                ValidAudience = Configuration["JwtSettings:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}
