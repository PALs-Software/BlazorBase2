using System.Security.Claims;
using System.Security.Cryptography;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// A refresh token as looked up by its hash, before any mutation - lets a caller inspect it (unknown,
/// revoked, expired) before deciding whether to consume it.
/// </summary>
public sealed record StoredRefreshToken(Guid Id, string UserId, Guid FamilyId, string SecurityStamp, DateTime ExpiresAt, bool IsRevoked);

/// <summary>
/// Mints access tokens through the bearer scheme's own protector, unchanged from before, and refresh
/// tokens as hashed, rotating database records with family-based reuse detection - the OAuth 2.0
/// Security BCP scheme for public clients. An access token stays a pure protector round-trip and
/// carries no database footprint; a refresh token additionally leaves a row behind so a stolen one can
/// be told apart from a legitimate rotation.
/// </summary>
public class AuthTokenIssuer<TUser>(
    IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
    BlazorBaseUserServerOptions serverOptions,
    IOptions<IdentityOptions> identityOptions,
    TimeProvider timeProvider,
    BaseUserDbContext<TUser> dbContext,
    SignInManager<TUser> signInManager,
    ILogger<AuthTokenIssuer<TUser>> logger)
    where TUser : BaseUser
{
    #region Injects
    private readonly BearerTokenOptions Options = bearerTokenOptions.Get(serverOptions.SchemeName);
    private readonly string SecurityStampClaimType = identityOptions.Value.ClaimsIdentity.SecurityStampClaimType;
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly BaseUserDbContext<TUser> DbContext = dbContext;
    private readonly SignInManager<TUser> SignInManager = signInManager;
    private readonly ILogger<AuthTokenIssuer<TUser>> Logger = logger;
    #endregion

    /// <summary>
    /// Mints a fresh access/refresh token pair for <paramref name="principal"/> and persists the
    /// refresh token's hash under <paramref name="familyId"/> (a new family when omitted, continuing
    /// an existing one on rotation).
    /// </summary>
    public async Task<LoginResponse> IssueTokenPairAsync(ClaimsPrincipal principal, Guid? familyId = null, CancellationToken cancellationToken = default)
    {
        var response = BuildTokenPair(principal, familyId);
        await DbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    /// <summary>
    /// Looks up a presented refresh token by its hash without mutating anything, so a caller can
    /// decide - unknown, already revoked (a replay), expired, or usable - before touching the row.
    /// </summary>
    public async Task<StoredRefreshToken?> FindAsync(string presentedToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashRefreshToken(presentedToken);

        return await DbContext.RefreshTokens
            .AsNoTracking()
            .Where(token => token.Token == tokenHash)
            .Select(token => new StoredRefreshToken(token.Id, token.UserId, token.FamilyId, token.SecurityStamp, token.ExpiresAt, token.IsRevoked))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public bool IsExpired(StoredRefreshToken stored) => stored.ExpiresAt < TimeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Re-checks that <paramref name="stored"/>'s captured security stamp still matches the user's
    /// current one, exactly as <see cref="SignInManager{TUser}.ValidateSecurityStampAsync"/> does for
    /// a cookie session - so a stamp rotated elsewhere (a password change, an explicit sign-out of
    /// every session) invalidates a refresh token minted before it, even though the token's own row is
    /// untouched by that rotation.
    /// </summary>
    public async Task<TUser?> ValidateSecurityStampAsync(StoredRefreshToken stored)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, stored.UserId),
            new Claim(SecurityStampClaimType, stored.SecurityStamp),
        ]));

        return await SignInManager.ValidateSecurityStampAsync(principal);
    }

    /// <summary>
    /// Revokes every unrevoked token in <paramref name="familyId"/> without logging - the routine
    /// case (logout, password change), as opposed to <see cref="RevokeFamilyAfterReuseDetectedAsync"/>.
    /// </summary>
    public async Task RevokeFamilyAsync(string userId, Guid familyId, CancellationToken cancellationToken = default)
    {
        await DbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.UserId == userId && !token.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.IsRevoked, true), cancellationToken);
    }

    /// <summary>
    /// Revokes an entire token family after a replay was detected and logs it - a revoked token being
    /// presented again means whoever holds it now is not the party the last legitimate rotation went
    /// to, the theft signal the OAuth 2.0 Security BCP rotation scheme exists to surface.
    /// </summary>
    public async Task RevokeFamilyAfterReuseDetectedAsync(string userId, Guid familyId, CancellationToken cancellationToken = default)
    {
        await RevokeFamilyAsync(userId, familyId, cancellationToken);
        Logger.LogWarning("Refresh-token reuse detected for user {UserId}; revoking token family {FamilyId}.", userId, familyId);
    }

    /// <summary>
    /// Atomically revokes <paramref name="stored"/> and issues its replacement in the same family, or
    /// returns <see langword="null"/> when a concurrent refresh already revoked it first - the
    /// affected-row-count guard is what makes two concurrent refreshes of the same token unable to
    /// both succeed.
    /// </summary>
    public async Task<LoginResponse?> RotateAsync(StoredRefreshToken stored, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var strategy = DbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync<LoginResponse?>(async () =>
        {
            if (attempt++ > 0)
                DetachPendingRefreshTokens();

            await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);

            var revoked = await DbContext.RefreshTokens
                .Where(token => token.Id == stored.Id && !token.IsRevoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.IsRevoked, true), cancellationToken);

            if (revoked == 0)
                return null;

            var response = BuildTokenPair(principal, stored.FamilyId);
            await DbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        });
    }

    private void DetachPendingRefreshTokens()
    {
        foreach (var entry in DbContext.ChangeTracker.Entries<RefreshToken>().Where(entry => entry.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;
    }

    private LoginResponse BuildTokenPair(ClaimsPrincipal principal, Guid? familyId)
    {
        var now = TimeProvider.GetUtcNow();
        var accessExpiresUtc = now + Options.BearerTokenExpiration;
        var refreshExpiresUtc = now + Options.RefreshTokenExpiration;
        var refreshTokenValue = GenerateRefreshTokenValue();

        DbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = HashRefreshToken(refreshTokenValue),
            UserId = RequireUserId(principal),
            FamilyId = familyId is null || familyId.Value == Guid.Empty ? Guid.NewGuid() : familyId.Value,
            SecurityStamp = principal.FindFirstValue(SecurityStampClaimType) ?? string.Empty,
            ExpiresAt = refreshExpiresUtc.UtcDateTime,
            CreatedAt = now.UtcDateTime,
        });

        return new LoginResponse
        {
            AccessToken = Options.BearerTokenProtector.Protect(CreateTicket(principal, accessExpiresUtc)),
            RefreshToken = refreshTokenValue,
            ExpiresAt = accessExpiresUtc,
        };
    }

    private static string RequireUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The principal carries no name identifier claim to mint a refresh token for.");

    private static string GenerateRefreshTokenValue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static AuthenticationTicket CreateTicket(ClaimsPrincipal principal, DateTimeOffset expiresUtc) =>
        new(principal, new AuthenticationProperties { ExpiresUtc = expiresUtc }, IdentityConstants.BearerScheme);
}
