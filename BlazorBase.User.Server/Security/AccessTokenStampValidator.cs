using System.Security.Claims;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Security;

/// <summary>
/// Delegates to <see cref="SignInManager{TUser}.ValidateSecurityStampAsync(ClaimsPrincipal)"/> rather
/// than hand-rolling the claim comparison, so the claim type, the null-stamp case and the user lookup
/// all follow Identity's own semantics - the same call
/// <see cref="BlazorBase.User.Server.Services.AuthTokenIssuer{TUser}"/> already uses to validate a
/// presented refresh token's captured stamp.
/// </summary>
public sealed class AccessTokenStampValidator<TUser>(SignInManager<TUser> signInManager) : IAccessTokenStampValidator
    where TUser : BaseUser
{
    public async Task<bool> IsCurrentAsync(ClaimsPrincipal principal) =>
        await signInManager.ValidateSecurityStampAsync(principal) is not null;
}
