using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Invitations.Configuration;
using BlazorBase.User.Server.Invitations.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Invitations;

/// <summary>
/// The invitation flow behind <c>MapBlazorBaseUserInvitationEndpoints</c>: create, list, resend, renew the
/// link, revoke, preview and accept. Every token is 32 random bytes; only its SHA-256 hash is stored, and
/// creating, resending or renewing replaces the hash, so an older link stops working at once.
/// </summary>
/// <typeparam name="TUser">The host's user type.</typeparam>
public sealed class UserInvitationService<TUser>(
    BaseUserDbContext<TUser> dbContext,
    UserManager<TUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IEnumerable<IUserInvitationHandler<TUser>> handlers,
    IUserInvitationEmailSender emailSender,
    IOptions<UserInvitationOptions> invitationOptions,
    IOptions<AccountManagementOptions> accountManagementOptions,
    TimeProvider timeProvider,
    ILogger<UserInvitationService<TUser>> logger)
    where TUser : BaseUser, new()
{
    #region Injects
    private readonly BaseUserDbContext<TUser> DbContext = dbContext;
    private readonly UserManager<TUser> UserManager = userManager;
    private readonly RoleManager<IdentityRole> RoleManager = roleManager;
    private readonly IReadOnlyList<IUserInvitationHandler<TUser>> Handlers = [.. handlers];
    private readonly IUserInvitationEmailSender EmailSender = emailSender;
    private readonly IOptions<UserInvitationOptions> InvitationOptions = invitationOptions;
    private readonly IOptions<AccountManagementOptions> AccountManagementOptions = accountManagementOptions;
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly ILogger<UserInvitationService<TUser>> Logger = logger;
    #endregion

    private const int MaximumTokenLength = 200;
    private const string InvalidTokenMessage = "This invitation link is invalid or has expired.";
    private const string UserExistsMessage = "An account with this email address already exists.";
    private const string NotFoundMessage = "The invitation does not exist.";
    private const string NotPendingMessage = "The invitation has already been accepted or revoked.";

    private DateTime UtcNow => TimeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Returns every invitation <paramref name="administrator"/> may manage, newest first.</summary>
    public async Task<IReadOnlyList<UserInvitationSummary>> ListAsync(ClaimsPrincipal administrator, CancellationToken cancellationToken = default)
    {
        var invitations = await DbContext.UserInvitations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var utcNow = UtcNow;
        var summaries = new List<UserInvitationSummary>();

        foreach (var invitation in invitations.OrderByDescending(invitation => invitation.CreatedAt))
        {
            if (await CanManageAsync(invitation, administrator, cancellationToken))
                summaries.Add(ToSummary(invitation, utcNow));
        }

        return summaries;
    }

    /// <summary>
    /// Creates an invitation and sends its email. Refuses an address that already has an account
    /// (<see cref="UserInvitationOutcome.Conflict"/>); revokes any earlier open invitation for the same
    /// address, so only the newest link works.
    /// </summary>
    public async Task<UserInvitationResult<UserInvitationLinkResponse>> CreateAsync(
        CreateUserInvitationRequest request,
        ClaimsPrincipal administrator,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = Validate(request);
        if (validationErrors is not null)
            return ValidationFailure<UserInvitationLinkResponse>(validationErrors);

        var email = request.Email.Trim();

        if (await UserManager.FindByEmailAsync(email) is not null)
            return Failure<UserInvitationLinkResponse>(UserInvitationOutcome.Conflict, UserInvitationErrorCodes.UserExists, UserExistsMessage);

        var role = await RoleManager.FindByNameAsync(request.Role.Trim());
        if (role?.Name is null)
            return Failure<UserInvitationLinkResponse>(UserInvitationOutcome.Invalid, UserInvitationErrorCodes.UnknownRole, $"The role '{request.Role}' does not exist.");

        var utcNow = UtcNow;
        var token = UserInvitationTokenGenerator.CreateToken();

        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = UserManager.NormalizeEmail(email),
            DisplayName = NullIfWhiteSpace(request.DisplayName),
            Role = role.Name,
            Context = NullIfWhiteSpace(request.Context),
            Language = NullIfWhiteSpace(request.Language),
            TokenHash = UserInvitationTokenGenerator.Hash(token),
            CreatedAt = utcNow,
            ExpiresAt = utcNow.AddDays(LifetimeDays),
            InvitedByUserId = administrator.FindFirstValue(ClaimTypes.NameIdentifier),
        };

        foreach (var handler in Handlers)
        {
            var handlerResult = await handler.OnCreatingAsync(invitation, administrator, cancellationToken);
            if (!handlerResult.Succeeded)
                return Failure<UserInvitationLinkResponse>(UserInvitationOutcome.Rejected, UserInvitationErrorCodes.Rejected, handlerResult.ErrorMessage ?? "The invitation was refused.");
        }

        if (invitation.Context?.Length > UserInvitation.MaximumContextLength)
            return ValidationFailure<UserInvitationLinkResponse>(new Dictionary<string, string[]> { [nameof(CreateUserInvitationRequest.Context)] = [$"The context must not exceed {UserInvitation.MaximumContextLength} characters."] });

        DbContext.UserInvitations.Add(invitation);
        await DbContext.SaveChangesAsync(cancellationToken);

        await DbContext.UserInvitations
            .Where(existing => existing.NormalizedEmail == invitation.NormalizedEmail && existing.Id != invitation.Id && existing.AcceptedAt == null && existing.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(existing => existing.RevokedAt, utcNow), cancellationToken);

        return UserInvitationResult<UserInvitationLinkResponse>.Success(await RespondWithLinkAsync(invitation, token, sendEmail: true, cancellationToken));
    }

    /// <summary>Gives an open or expired invitation a new token and expiry and sends the email again.</summary>
    public Task<UserInvitationResult<UserInvitationLinkResponse>> ResendAsync(Guid invitationId, ClaimsPrincipal administrator, CancellationToken cancellationToken = default) =>
        RenewAsync(invitationId, administrator, sendEmail: true, cancellationToken);

    /// <summary>Gives an open or expired invitation a new token and expiry without sending an email.</summary>
    public Task<UserInvitationResult<UserInvitationLinkResponse>> RenewLinkAsync(Guid invitationId, ClaimsPrincipal administrator, CancellationToken cancellationToken = default) =>
        RenewAsync(invitationId, administrator, sendEmail: false, cancellationToken);

    /// <summary>Withdraws an invitation that was not accepted yet; revoking twice succeeds.</summary>
    public async Task<UserInvitationResult<bool>> RevokeAsync(Guid invitationId, ClaimsPrincipal administrator, CancellationToken cancellationToken = default)
    {
        var invitation = await FindManageableAsync(invitationId, administrator, cancellationToken);
        if (invitation is null)
            return Failure<bool>(UserInvitationOutcome.NotFound, UserInvitationErrorCodes.NotFound, NotFoundMessage);

        if (invitation.AcceptedAt is not null)
            return Failure<bool>(UserInvitationOutcome.Conflict, UserInvitationErrorCodes.NotPending, NotPendingMessage);

        if (invitation.RevokedAt is null)
        {
            invitation.RevokedAt = UtcNow;
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return UserInvitationResult<bool>.Success(true);
    }

    /// <summary>
    /// What the accept page may show for <paramref name="token"/>. Unknown, expired, revoked and used
    /// tokens all answer an empty preview with <see cref="UserInvitationPreview.IsValid"/> false.
    /// </summary>
    public async Task<UserInvitationPreview> PreviewAsync(string? token, CancellationToken cancellationToken = default)
    {
        var invitation = await FindByTokenAsync(token, cancellationToken);

        if (invitation is null || !IsOpen(invitation, UtcNow))
            return new UserInvitationPreview();

        return new UserInvitationPreview
        {
            IsValid = true,
            Email = invitation.Email,
            DisplayName = invitation.DisplayName,
            ExpiresAt = invitation.ExpiresAt,
        };
    }

    /// <summary>
    /// Redeems a token: creates the active account with a confirmed email, the invited role and the
    /// invitation's language, marks the invitation accepted and runs every handler's
    /// <see cref="IUserInvitationHandler{TUser}.OnAcceptedAsync"/>, all in one transaction. The token is
    /// claimed with a conditional update, so of two concurrent requests only one creates an account.
    /// </summary>
    public async Task<UserInvitationResult<TUser>> AcceptAsync(AcceptUserInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = Validate(request);
        if (validationErrors is not null)
            return ValidationFailure<TUser>(validationErrors);

        var invitation = await FindByTokenAsync(request.Token, cancellationToken);
        if (invitation is null || !IsOpen(invitation, UtcNow))
            return Failure<TUser>(UserInvitationOutcome.Invalid, UserInvitationErrorCodes.InvalidToken, InvalidTokenMessage);

        if (await UserManager.FindByEmailAsync(invitation.Email) is not null)
            return Failure<TUser>(UserInvitationOutcome.Conflict, UserInvitationErrorCodes.UserExists, UserExistsMessage);

        var strategy = DbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
                DbContext.ChangeTracker.Clear();

            return await AcceptInTransactionAsync(invitation, request, cancellationToken);
        });
    }

    private async Task<UserInvitationResult<TUser>> AcceptInTransactionAsync(UserInvitation invitation, AcceptUserInvitationRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);

        var utcNow = UtcNow;
        var user = new TUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            DisplayName = request.DisplayName.Trim(),
            EmailConfirmed = true,
            IsActive = true,
        };

        if (!string.IsNullOrWhiteSpace(invitation.Language))
            user.Language = invitation.Language;

        var claimedRows = await DbContext.UserInvitations
            .Where(candidate => candidate.Id == invitation.Id
                && candidate.TokenHash == invitation.TokenHash
                && candidate.AcceptedAt == null
                && candidate.RevokedAt == null
                && candidate.ExpiresAt > utcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.AcceptedAt, utcNow)
                .SetProperty(candidate => candidate.AcceptedUserId, user.Id), cancellationToken);

        if (claimedRows != 1)
            return await RollBackAsync(transaction, Failure<TUser>(UserInvitationOutcome.Invalid, UserInvitationErrorCodes.InvalidToken, InvalidTokenMessage));

        var createResult = await UserManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return await RollBackAsync(transaction, ToCreateFailure(createResult));

        var roleResult = await UserManager.AddToRoleAsync(user, invitation.Role);
        if (!roleResult.Succeeded)
            return await RollBackAsync(transaction, Failure<TUser>(UserInvitationOutcome.Invalid, UserInvitationErrorCodes.UnknownRole, $"The role '{invitation.Role}' does not exist."));

        invitation.AcceptedAt = utcNow;
        invitation.AcceptedUserId = user.Id;

        foreach (var handler in Handlers)
        {
            var handlerResult = await handler.OnAcceptedAsync(user, invitation, cancellationToken);
            if (!handlerResult.Succeeded)
                return await RollBackAsync(transaction, Failure<TUser>(UserInvitationOutcome.Rejected, UserInvitationErrorCodes.Rejected, handlerResult.ErrorMessage ?? "The invitation was refused."));
        }

        await transaction.CommitAsync(cancellationToken);
        return UserInvitationResult<TUser>.Success(user);
    }

    private static async Task<UserInvitationResult<TUser>> RollBackAsync(IDbContextTransaction transaction, UserInvitationResult<TUser> failure)
    {
        await transaction.RollbackAsync();
        return failure;
    }

    /// <summary>
    /// Password policy failures answer as <see cref="UserInvitationErrorCodes.PasswordRejected"/> with the
    /// reasons under <c>Password</c>; a duplicate name or address (a concurrent account) as a conflict.
    /// </summary>
    private static UserInvitationResult<TUser> ToCreateFailure(IdentityResult createResult)
    {
        var errors = createResult.Errors.ToList();

        if (errors.Any(error => error.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail)))
            return Failure<TUser>(UserInvitationOutcome.Conflict, UserInvitationErrorCodes.UserExists, UserExistsMessage);

        var passwordErrors = errors.Where(error => error.Code.StartsWith("Password", StringComparison.Ordinal)).Select(error => error.Description).ToArray();
        if (passwordErrors.Length > 0)
            return Failure<TUser>(UserInvitationOutcome.Invalid, UserInvitationErrorCodes.PasswordRejected, string.Join(" ", passwordErrors),
                new Dictionary<string, string[]> { [nameof(AcceptUserInvitationRequest.Password)] = passwordErrors });

        var descriptions = errors.Select(error => error.Description).ToArray();
        return Failure<TUser>(UserInvitationOutcome.Invalid, UserInvitationErrorCodes.ValidationFailed, string.Join(" ", descriptions),
            new Dictionary<string, string[]> { [string.Empty] = descriptions });
    }

    private async Task<UserInvitationResult<UserInvitationLinkResponse>> RenewAsync(Guid invitationId, ClaimsPrincipal administrator, bool sendEmail, CancellationToken cancellationToken)
    {
        var invitation = await FindManageableAsync(invitationId, administrator, cancellationToken);
        if (invitation is null)
            return Failure<UserInvitationLinkResponse>(UserInvitationOutcome.NotFound, UserInvitationErrorCodes.NotFound, NotFoundMessage);

        if (invitation.AcceptedAt is not null || invitation.RevokedAt is not null)
            return Failure<UserInvitationLinkResponse>(UserInvitationOutcome.Conflict, UserInvitationErrorCodes.NotPending, NotPendingMessage);

        if (await UserManager.FindByEmailAsync(invitation.Email) is not null)
            return Failure<UserInvitationLinkResponse>(UserInvitationOutcome.Conflict, UserInvitationErrorCodes.UserExists, UserExistsMessage);

        var token = UserInvitationTokenGenerator.CreateToken();
        invitation.TokenHash = UserInvitationTokenGenerator.Hash(token);
        invitation.ExpiresAt = UtcNow.AddDays(LifetimeDays);
        await DbContext.SaveChangesAsync(cancellationToken);

        return UserInvitationResult<UserInvitationLinkResponse>.Success(await RespondWithLinkAsync(invitation, token, sendEmail, cancellationToken));
    }

    private async Task<UserInvitationLinkResponse> RespondWithLinkAsync(UserInvitation invitation, string token, bool sendEmail, CancellationToken cancellationToken)
    {
        var link = BuildAcceptLink(token);
        var emailSent = sendEmail && await TrySendEmailAsync(invitation, link, cancellationToken);

        if (emailSent)
        {
            invitation.LastSentAt = UtcNow;
            invitation.SendCount++;
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return new UserInvitationLinkResponse
        {
            Invitation = ToSummary(invitation, UtcNow),
            Link = link,
            EmailSent = emailSent,
        };
    }

    /// <summary>
    /// A failing mail pipeline must not undo the invitation: the administrator is told the email was not
    /// sent and copies the link instead. The address is not logged.
    /// </summary>
    private async Task<bool> TrySendEmailAsync(UserInvitation invitation, string link, CancellationToken cancellationToken)
    {
        try
        {
            var email = new UserInvitationEmail
            {
                RecipientEmail = invitation.Email,
                RecipientName = invitation.DisplayName,
                InvitedByName = await ResolveInviterNameAsync(invitation.InvitedByUserId),
                AcceptLink = link,
                ExpiresAt = invitation.ExpiresAt,
                Language = invitation.Language ?? new TUser().Language,
            };

            return await EmailSender.SendAsync(email, cancellationToken);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "The invitation email for invitation {InvitationId} could not be sent.", invitation.Id);
            return false;
        }
    }

    private async Task<string?> ResolveInviterNameAsync(string? invitedByUserId)
    {
        if (string.IsNullOrEmpty(invitedByUserId))
            return null;

        var inviter = await UserManager.FindByIdAsync(invitedByUserId);
        return string.IsNullOrWhiteSpace(inviter?.DisplayName) ? null : inviter.DisplayName;
    }

    private string BuildAcceptLink(string token)
    {
        var clientBaseUrl = AccountManagementOptions.Value.ClientBaseUrl?.TrimEnd('/');
        var acceptPagePath = InvitationOptions.Value.AcceptPagePath.Trim('/');

        return $"{clientBaseUrl}/{acceptPagePath}?token={Uri.EscapeDataString(token)}";
    }

    private async Task<UserInvitation?> FindManageableAsync(Guid invitationId, ClaimsPrincipal administrator, CancellationToken cancellationToken)
    {
        var invitation = await DbContext.UserInvitations.FirstOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);

        if (invitation is null)
            return null;

        return await CanManageAsync(invitation, administrator, cancellationToken) ? invitation : null;
    }

    private async Task<bool> CanManageAsync(UserInvitation invitation, ClaimsPrincipal administrator, CancellationToken cancellationToken)
    {
        foreach (var handler in Handlers)
        {
            if (!await handler.CanManageAsync(invitation, administrator, cancellationToken))
                return false;
        }

        return true;
    }

    private async Task<UserInvitation?> FindByTokenAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaximumTokenLength)
            return null;

        var tokenHash = UserInvitationTokenGenerator.Hash(token);
        var invitation = await DbContext.UserInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        if (invitation is null || !UserInvitationTokenGenerator.MatchesHash(token, invitation.TokenHash))
            return null;

        return invitation;
    }

    private int LifetimeDays => Math.Max(InvitationOptions.Value.LifetimeDays, 1);

    private static bool IsOpen(UserInvitation invitation, DateTime utcNow) =>
        invitation.AcceptedAt is null && invitation.RevokedAt is null && invitation.ExpiresAt > utcNow;

    private static UserInvitationStatus ResolveStatus(UserInvitation invitation, DateTime utcNow)
    {
        if (invitation.AcceptedAt is not null)
            return UserInvitationStatus.Accepted;

        if (invitation.RevokedAt is not null)
            return UserInvitationStatus.Revoked;

        return invitation.ExpiresAt > utcNow ? UserInvitationStatus.Pending : UserInvitationStatus.Expired;
    }

    private static UserInvitationSummary ToSummary(UserInvitation invitation, DateTime utcNow) => new()
    {
        Id = invitation.Id,
        Email = invitation.Email,
        DisplayName = invitation.DisplayName,
        Role = invitation.Role,
        Context = invitation.Context,
        Language = invitation.Language,
        Status = ResolveStatus(invitation, utcNow),
        CreatedAt = invitation.CreatedAt,
        ExpiresAt = invitation.ExpiresAt,
        InvitedByUserId = invitation.InvitedByUserId,
        AcceptedAt = invitation.AcceptedAt,
        AcceptedUserId = invitation.AcceptedUserId,
        RevokedAt = invitation.RevokedAt,
        LastSentAt = invitation.LastSentAt,
        SendCount = invitation.SendCount,
    };

    private static Dictionary<string, string[]>? Validate(object request)
    {
        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true))
            return null;

        return validationResults
            .SelectMany(result => (result.MemberNames.Any() ? result.MemberNames : [string.Empty]).Select(member => (Member: member, Message: result.ErrorMessage ?? string.Empty)))
            .GroupBy(entry => entry.Member)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Message).ToArray());
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static UserInvitationResult<TValue> Failure<TValue>(UserInvitationOutcome outcome, string errorCode, string errorMessage, IDictionary<string, string[]>? errors = null) =>
        UserInvitationResult<TValue>.Failure(outcome, errorCode, errorMessage, errors);

    private static UserInvitationResult<TValue> ValidationFailure<TValue>(IDictionary<string, string[]> errors) =>
        UserInvitationResult<TValue>.Failure(UserInvitationOutcome.Invalid, UserInvitationErrorCodes.ValidationFailed, "The request is invalid.", errors);
}
