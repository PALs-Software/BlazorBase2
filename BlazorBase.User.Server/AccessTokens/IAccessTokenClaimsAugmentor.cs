using System.Security.Claims;

namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Seam for turning a validated access token's bindings into claims on the authenticated
/// principal. Mirrors <see cref="Services.IClaimsAugmentor{TUser}"/> - the equivalent seam for the
/// JWT issued to interactive users - but for machine tokens. Register implementations via
/// <see cref="BlazorBaseAccessTokenServiceCollectionExtensions.AddAccessTokenClaimsAugmentor{TAugmentor}"/>.
/// </summary>
public interface IAccessTokenClaimsAugmentor
{
    /// <summary>
    /// Returns extra claims to append to the principal for the given validated token. Called after
    /// the built-in claims (<see cref="AccessTokenClaimTypes.TokenId"/>, <see cref="ClaimTypes.Name"/>,
    /// <see cref="ClaimTypes.NameIdentifier"/>) are assembled; an empty return value is a no-op.
    /// </summary>
    Task<IEnumerable<Claim>> GetAdditionalClaimsAsync(ValidatedAccessToken validatedAccessToken);
}
