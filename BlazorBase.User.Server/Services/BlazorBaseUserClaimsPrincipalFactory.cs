using System.Security.Claims;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Builds the claims for a <typeparamref name="TUser"/>: the two-generic base already supplies name
/// identifier, security stamp and per-role claims from the real store; this override adds the
/// framework's own display name, theme, language and every registered
/// <see cref="IClaimsAugmentor{TUser}"/>'s claims. Registered via <c>AddClaimsPrincipalFactory</c> so
/// <see cref="TokenService{TUser}"/> and any future cookie sign-in share one claim set.
/// </summary>
public class BlazorBaseUserClaimsPrincipalFactory<TUser>(
    UserManager<TUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor,
    IEnumerable<IClaimsAugmentor<TUser>> claimsAugmentors)
    : UserClaimsPrincipalFactory<TUser, IdentityRole>(userManager, roleManager, optionsAccessor)
    where TUser : BaseUser
{
    #region Injects
    private readonly IEnumerable<IClaimsAugmentor<TUser>> ClaimsAugmentors = claimsAugmentors;
    #endregion

    /// <summary>
    /// The base factory does not add an email claim by default, so it is added explicitly here -
    /// everything else (name identifier, security stamp, role claims) already comes from the base.
    /// </summary>
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(TUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email ?? string.Empty));
        identity.AddClaim(new Claim("displayName", user.DisplayName));
        identity.AddClaim(new Claim("themePreference", user.ThemePreference));
        identity.AddClaim(new Claim("language", user.Language));

        var roles = await UserManager.GetRolesAsync(user);

        foreach (var augmentor in ClaimsAugmentors)
        {
            var additionalClaims = await augmentor.GetAdditionalClaimsAsync(user, roles);
            identity.AddClaims(additionalClaims);
        }

        return identity;
    }
}
