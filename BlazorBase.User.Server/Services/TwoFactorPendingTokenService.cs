using System.Buffers.Text;
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
/// token can never be presented as an access token or a refresh token, or the reverse. A second claim
/// carries a random single-use id tracked by <see cref="ITwoFactorPendingTokenStore"/>, so the token
/// can complete at most one login even while it is still within its own lifetime.
/// </remarks>
public class TwoFactorPendingTokenService<TUser>(
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider,
    ITwoFactorPendingTokenStore store)
    where TUser : BaseUser
{
    internal const string TokenIdClaimType = "BlazorBase.User.TwoFactorPending.TokenId";
    private const string ProtectorPurpose = "BlazorBase.User.TwoFactorPending.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    #region Injects
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly ITwoFactorPendingTokenStore Store = store;
    #endregion

    private readonly ISecureDataFormat<AuthenticationTicket> Format =
        new TicketDataFormat(dataProtectionProvider.CreateProtector(ProtectorPurpose));

    /// <summary>
    /// Mints a pending token carrying only <paramref name="user"/>'s id, a fresh single-use token id,
    /// and an expiry a few minutes out - nothing an eavesdropper could use beyond completing this one
    /// pending sign-in, once.
    /// </summary>
    public string Protect(TUser user)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(TokenIdClaimType, GenerateTokenId()),
            ],
            "TwoFactorPending"));

        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties { ExpiresUtc = TimeProvider.GetUtcNow() + Lifetime },
            "BlazorBase.TwoFactorPending");

        return Format.Protect(ticket);
    }

    /// <summary>
    /// Returns the user id carried by <paramref name="pendingToken"/>, or <see langword="null"/> when
    /// it does not unprotect (wrong purpose, tampered, or minted under a different key ring), has
    /// already expired, or its token id was already consumed by a completed login. Never throws and
    /// never distinguishes these outcomes: to the caller, all of them mean "sign in again". A pure
    /// read - it never marks the token consumed; call <see cref="ConsumeAsync"/> once the login this
    /// token guards actually succeeds.
    /// </summary>
    public async Task<string?> UnprotectAsync(string pendingToken, CancellationToken cancellationToken = default)
    {
        var ticket = TryUnprotectValidTicket(pendingToken);
        if (ticket is null)
            return null;

        var tokenId = ticket.Principal.FindFirstValue(TokenIdClaimType);
        if (string.IsNullOrEmpty(tokenId) || await Store.IsConsumedAsync(tokenId, cancellationToken))
            return null;

        return ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Marks <paramref name="pendingToken"/>'s id as consumed, so it is rejected by
    /// <see cref="UnprotectAsync"/> from now on even though it has not yet expired. Call this only
    /// once the two-factor login it guards has actually succeeded - a wrong code must never consume
    /// it, or a typo would force the user back to a full password login.
    /// </summary>
    public async Task<bool> ConsumeAsync(string pendingToken, CancellationToken cancellationToken = default)
    {
        var ticket = TryUnprotectValidTicket(pendingToken);
        if (ticket is null)
            return false;

        var tokenId = ticket.Principal.FindFirstValue(TokenIdClaimType);
        return !string.IsNullOrEmpty(tokenId) && await Store.TryConsumeAsync(tokenId, ticket.Properties.ExpiresUtc!.Value, cancellationToken);
    }

    private AuthenticationTicket? TryUnprotectValidTicket(string pendingToken)
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

        return ticket;
    }

    private static string GenerateTokenId() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
}
