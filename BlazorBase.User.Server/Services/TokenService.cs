using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BlazorBase.User.Server.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BlazorBase.User.Server.Services;

public class TokenService<TUser>(IConfiguration configuration, IEnumerable<IClaimsAugmentor<TUser>> claimsAugmentors) where TUser : BaseUser
{
    #region Injects
    private readonly IConfiguration Configuration = configuration;
    private readonly IEnumerable<IClaimsAugmentor<TUser>> ClaimsAugmentors = claimsAugmentors;
    #endregion

    public async Task<string> GenerateAccessToken(TUser user, IList<string> roles)
    {
        var secret = Configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JwtSettings:Secret is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("displayName", user.DisplayName),
            new("themePreference", user.ThemePreference),
            new("language", user.Language),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var augmentor in ClaimsAugmentors)
        {
            var additionalClaims = await augmentor.GetAdditionalClaimsAsync(user, roles).ConfigureAwait(false);
            claims.AddRange(additionalClaims);
        }

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
