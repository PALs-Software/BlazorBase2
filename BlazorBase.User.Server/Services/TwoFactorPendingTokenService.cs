using System.Security.Claims;
using System.Security.Cryptography;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Mints and unprotects the short-lived, opaque token that carries state between <c>login</c>
/// answering "second factor required" and <c>login/2fa</c> completing it - deliberately not
/// <see cref="Microsoft.AspNetCore.Identity.IdentityConstants.TwoFactorUserIdScheme"/>, which is a
/// cookie and would make a native bearer client behave differently from a browser, and whose internal
/// claim shape is Identity's own implementation detail rather than public API.
/// </summary>
/// <remarks>
/// Built the same way <see cref="AuthTokenIssuer{TUser}"/> mints its own tokens - a data-protected
/// <see cref="AuthenticationTicket"/> through a <see cref="TicketDataFormat"/> - but under its own Data
/// Protection purpose. Each purpose derives an independent key from the same key ring, so a pending
/// token can never be presented as an access token or a refresh token, or the reverse.
/// </remarks>
public class TwoFactorPendingTokenService<TUser>(IDataProtectionProvider dataProtectionProvider, TimeProvider timeProvider)
    where TUser : BaseUser
{
    private const string ProtectorPurpose = "BlazorBase.User.TwoFactorPending.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    #region Injects
    private readonly TimeProvider TimeProvider = timeProvider;
    #endregion

    private readonly ISecureDataFormat<AuthenticationTicket> Format =
        new TicketDataFormat(dataProtectionProvider.CreateProtector(ProtectorPurpose));

    /// <summary>
    /// Mints a pending token carrying only <paramref name="user"/>'s id and an expiry a few minutes
    /// out - nothing an eavesdropper could use beyond completing this one pending sign-in.
    /// </summary>
    public string Protect(TUser user)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id)], "TwoFactorPending"));

        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties { ExpiresUtc = TimeProvider.GetUtcNow() + Lifetime },
            "BlazorBase.TwoFactorPending");

        return Format.Protect(ticket);
    }

    /// <summary>
    /// Returns the user id carried by <paramref name="pendingToken"/>, or <see langword="null"/> when
    /// it does not unprotect (wrong purpose, tampered, or minted under a different key ring) or has
    /// already expired. Never throws: to the caller, an invalid token and an expired one are the same
    /// outcome - "sign in again".
    /// </summary>
    public string? Unprotect(string pendingToken)
    {
        AuthenticationTicket? ticket;
        try
        {
            ticket = Format.Unprotect(pendingToken);
        }
        catch (CryptographicException)
        {
            return null;
        }

        if (ticket is null)
            return null;

        if (ticket.Properties.ExpiresUtc is null || ticket.Properties.ExpiresUtc < TimeProvider.GetUtcNow())
            return null;

        return ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
