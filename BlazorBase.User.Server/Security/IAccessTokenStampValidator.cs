using System.Security.Claims;

namespace BlazorBase.User.Server.Security;

/// <summary>
/// Re-checks whether a principal's captured security-stamp claim still matches the user's current
/// stamp, so <see cref="BlazorBaseAccessTokenStampValidationMiddleware"/> can stay independent of the
/// concrete <c>TUser</c> type <c>AddBlazorBaseUserServer&lt;TUser,TContext&gt;</c> was called with.
/// </summary>
public interface IAccessTokenStampValidator
{
    /// <summary>
    /// <see langword="true"/> when <paramref name="principal"/>'s stamp claim still matches its
    /// user's current one - a password reset, an explicit sign-out-everywhere, or any other stamp
    /// rotation makes this <see langword="false"/> for every principal minted before it.
    /// </summary>
    Task<bool> IsCurrentAsync(ClaimsPrincipal principal);
}
