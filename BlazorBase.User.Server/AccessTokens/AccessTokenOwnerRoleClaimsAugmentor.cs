using System.Security.Claims;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Adds the current roles of a token's owner to the access-token principal, so an endpoint can
/// authorize a machine token by the roles of the user it acts for. Opt in through
/// <see cref="BlazorBaseAccessTokenServiceCollectionExtensions.AddAccessTokenOwnerRoleClaims{TUser}"/>.
/// </summary>
/// <remarks>
/// The roles are read on every request rather than stamped on the token, so removing a role takes
/// effect on the owner's next call. A token without an owner, or whose owner no longer exists, gets no
/// roles; an inactive owner never gets this far, because validation already rejects that token.
/// </remarks>
public sealed class AccessTokenOwnerRoleClaimsAugmentor<TUser>(UserManager<TUser> userManager) : IAccessTokenClaimsAugmentor
    where TUser : BaseUser
{
    #region Injects
    private readonly UserManager<TUser> UserManager = userManager;
    #endregion

    public async Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(ValidatedAccessToken validatedAccessToken)
    {
        if (validatedAccessToken.UserId is null)
            return [];

        var owner = await UserManager.FindByIdAsync(validatedAccessToken.UserId);

        if (owner is null)
            return [];

        var roles = await UserManager.GetRolesAsync(owner);
        return roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
    }
}
