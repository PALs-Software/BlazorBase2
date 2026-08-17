using System.Security.Claims;
using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Seam for injecting additional claims into JWT access tokens.
/// Register implementations via <see cref="BlazorBaseUserServerServiceCollectionExtensions.AddClaimsAugmentor{TUser,TAugmentor}"/>.
/// </summary>
/// <typeparam name="TUser">The application user type.</typeparam>
public interface IClaimsAugmentor<TUser> where TUser : BaseUser
{
    /// <summary>
    /// Returns extra claims to append to the access token for the given user and roles.
    /// Called after the built-in claims are assembled; an empty return value is a no-op.
    /// </summary>
    Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(TUser user, IList<string> roles);
}
