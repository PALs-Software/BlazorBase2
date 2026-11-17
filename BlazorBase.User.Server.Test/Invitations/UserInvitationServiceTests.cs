using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Invitations;
using BlazorBase.User.Server.Invitations.Mail;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BlazorBase.User.Server.Test.Invitations;

/// <summary>
/// <see cref="UserInvitationService{TUser}"/> over a real Identity stack and SQLite: only the token hash is
/// stored, a link works once and until it expires, revoking and renewing invalidate it, accepting creates
/// an active account with role and confirmed email, and the application handler can refuse both steps.
/// </summary>
public sealed class UserInvitationServiceTests : IdentityServerTestBase
{
    private const string InvitedEmail = "new.person@example.com";

    private readonly RecordingInvitationEmailSender EmailSender = new();
    private readonly RecordingInvitationHandler Handler = new();

    protected override IDictionary<string, string?> AdditionalConfiguration => new Dictionary<string, string?>
    {
        ["Invitations:LifetimeDays"] = "7",
    };

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        services.AddLogging();
        services.Configure<AccountManagementOptions>(options => options.ClientBaseUrl = "https://app.example.com/");
        services.AddBlazorBaseUserInvitations<TestUser>();
        services.RemoveAll<IUserInvitationEmailSender>();
        services.AddSingleton<IUserInvitationEmailSender>(EmailSender);
        services.AddSingleton<IUserInvitationHandler<TestUser>>(Handler);
    }

    private async Task<(TestUser Administrator, ClaimsPrincipal Principal)> SeedAdministratorAsync()
    {
        var administrator = await SeedUserAsync("admin@example.com", "Admin Person", role: "Admin");
        return (administrator, PrincipalFor(administrator.Id));
    }

    private async Task<UserInvitationLinkResponse> CreateInvitationAsync(ClaimsPrincipal administrator, string email = InvitedEmail, string role = "User")
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<UserInvitationService<TestUser>>();
        var result = await service.CreateAsync(new CreateUserInvitationRequest { Email = email, DisplayName = "New Person", Role = role, Context = "household-1", Language = "de" }, administrator);

        Assert.True(result.Succeeded, result.ErrorMessage);
        return result.Value!;
    }

    private async Task<TResult> WithServiceAsync<TResult>(Func<UserInvitationService<TestUser>, Task<TResult>> action)
    {
        using var scope = CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<UserInvitationService<TestUser>>());
    }

    private static string TokenOf(UserInvitationLinkResponse response) => Uri.UnescapeDataString(response.Link[(response.Link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);

    private static AcceptUserInvitationRequest AcceptRequest(string token, string password = "Secret123456") =>
        new() { Token = token, DisplayName = "Chosen Name", Password = password };

    [Fact]
    public async Task Create_stores_only_the_hash_and_sends_the_link()
    {
        var (_, administrator) = await SeedAdministratorAsync();

        var response = await CreateInvitationAsync(administrator);

        Assert.StartsWith("https://app.example.com/invitation/accept?token=", response.Link);
        Assert.True(response.EmailSent);
        Assert.Equal(UserInvitationStatus.Pending, response.Invitation.Status);
        Assert.Equal(1, response.Invitation.SendCount);

        var token = TokenOf(response);
        Assert.Equal(43, token.Length);

        using var scope = CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<TestUserDbContext>().UserInvitations.SingleAsync();
        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(UserInvitationTokenGenerator.Hash(token), stored.TokenHash);
        Assert.Equal("NEW.PERSON@EXAMPLE.COM", stored.NormalizedEmail);
        Assert.Equal(TimeProvider.GetUtcNow().UtcDateTime.AddDays(7), stored.ExpiresAt);

        var email = Assert.Single(EmailSender.Emails);
        Assert.Equal(InvitedEmail, email.RecipientEmail);
        Assert.Equal("Admin Person", email.InvitedByName);
        Assert.Equal("de", email.Language);
        Assert.Equal(response.Link, email.AcceptLink);
    }

    [Fact]
    public async Task Create_without_a_mail_pipeline_still_succeeds_and_reports_it()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        EmailSender.Sends = false;

        var response = await CreateInvitationAsync(administrator);

        Assert.False(response.EmailSent);
        Assert.Equal(0, response.Invitation.SendCount);
        Assert.Null(response.Invitation.LastSentAt);
    }

    [Fact]
    public async Task Create_for_an_existing_account_is_a_conflict()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        await SeedUserAsync(InvitedEmail, "Existing");

        var result = await WithServiceAsync(service => service.CreateAsync(new CreateUserInvitationRequest { Email = InvitedEmail, Role = "User" }, administrator));

        Assert.Equal(UserInvitationOutcome.Conflict, result.Outcome);
        Assert.Equal(UserInvitationErrorCodes.UserExists, result.ErrorCode);
        Assert.Empty(EmailSender.Emails);
    }

    [Fact]
    public async Task Create_with_an_unknown_role_or_an_invalid_address_is_refused()
    {
        var (_, administrator) = await SeedAdministratorAsync();

        var unknownRole = await WithServiceAsync(service => service.CreateAsync(new CreateUserInvitationRequest { Email = InvitedEmail, Role = "Owner" }, administrator));
        var invalidAddress = await WithServiceAsync(service => service.CreateAsync(new CreateUserInvitationRequest { Email = "not-an-address", Role = "User" }, administrator));

        Assert.Equal(UserInvitationErrorCodes.UnknownRole, unknownRole.ErrorCode);
        Assert.Equal(UserInvitationErrorCodes.ValidationFailed, invalidAddress.ErrorCode);
        Assert.Contains(nameof(CreateUserInvitationRequest.Email), invalidAddress.Errors!.Keys);
    }

    [Fact]
    public async Task Create_replaces_an_open_invitation_for_the_same_address()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var first = await CreateInvitationAsync(administrator);

        var second = await CreateInvitationAsync(administrator, email: "New.Person@Example.com");

        var invitations = await WithServiceAsync(service => service.ListAsync(administrator));
        Assert.Equal(UserInvitationStatus.Revoked, invitations.Single(invitation => invitation.Id == first.Invitation.Id).Status);
        Assert.Equal(UserInvitationStatus.Pending, invitations.Single(invitation => invitation.Id == second.Invitation.Id).Status);
        Assert.False((await WithServiceAsync(service => service.PreviewAsync(TokenOf(first)))).IsValid);
    }

    [Fact]
    public async Task Handler_can_set_the_context_or_refuse_the_invitation()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        Handler.ContextToSet = "household-42";

        var created = await CreateInvitationAsync(administrator);
        Assert.Equal("household-42", created.Invitation.Context);

        Handler.CreatingRejection = "Not your household.";
        var refused = await WithServiceAsync(service => service.CreateAsync(new CreateUserInvitationRequest { Email = "other@example.com", Role = "User" }, administrator));

        Assert.Equal(UserInvitationOutcome.Rejected, refused.Outcome);
        Assert.Equal("Not your household.", refused.ErrorMessage);
        Assert.Single(await WithServiceAsync(service => service.ListAsync(administrator)));
    }

    [Fact]
    public async Task Handler_limits_which_invitations_an_administrator_manages()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);
        Handler.ManageableContext = "another-household";

        Assert.Empty(await WithServiceAsync(service => service.ListAsync(administrator)));
        Assert.Equal(UserInvitationOutcome.NotFound, (await WithServiceAsync(service => service.RevokeAsync(created.Invitation.Id, administrator))).Outcome);
        Assert.Equal(UserInvitationOutcome.NotFound, (await WithServiceAsync(service => service.ResendAsync(created.Invitation.Id, administrator))).Outcome);
    }

    [Fact]
    public async Task Preview_shows_the_invitation_for_a_valid_token_only()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);

        var valid = await WithServiceAsync(service => service.PreviewAsync(TokenOf(created)));
        var unknown = await WithServiceAsync(service => service.PreviewAsync("unknown-token"));
        var empty = await WithServiceAsync(service => service.PreviewAsync(null));

        Assert.True(valid.IsValid);
        Assert.Equal(InvitedEmail, valid.Email);
        Assert.Equal("New Person", valid.DisplayName);
        Assert.False(unknown.IsValid);
        Assert.Null(unknown.Email);
        Assert.Null(unknown.ExpiresAt);
        Assert.False(empty.IsValid);
    }

    [Fact]
    public async Task Accept_creates_an_active_confirmed_account_with_the_role_and_language()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);

        var result = await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(created))));

        Assert.True(result.Succeeded, result.ErrorMessage);

        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByEmailAsync(InvitedEmail);
        Assert.NotNull(user);
        Assert.Equal(InvitedEmail, user.UserName);
        Assert.Equal("Chosen Name", user.DisplayName);
        Assert.True(user.EmailConfirmed);
        Assert.True(user.IsActive);
        Assert.Equal("de", user.Language);
        Assert.True(await userManager.IsInRoleAsync(user, "User"));
        Assert.True(await userManager.CheckPasswordAsync(user, "Secret123456"));

        var (handledUser, handledInvitation) = Assert.Single(Handler.AcceptedCalls);
        Assert.Equal(user.Id, handledUser.Id);
        Assert.Equal(user.Id, handledInvitation.AcceptedUserId);
        Assert.Equal("household-1", handledInvitation.Context);

        var invitation = Assert.Single(await WithServiceAsync(service => service.ListAsync(administrator)));
        Assert.Equal(UserInvitationStatus.Accepted, invitation.Status);
        Assert.Equal(user.Id, invitation.AcceptedUserId);
    }

    [Fact]
    public async Task A_link_works_only_once()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);

        var first = await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(created))));
        var second = await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(created))));

        Assert.True(first.Succeeded);
        Assert.Equal(UserInvitationErrorCodes.InvalidToken, second.ErrorCode);
        Assert.False((await WithServiceAsync(service => service.PreviewAsync(TokenOf(created)))).IsValid);
    }

    [Fact]
    public async Task An_expired_link_is_refused_until_it_is_renewed()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);

        TimeProvider.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1));

        var expired = await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(created))));
        Assert.Equal(UserInvitationErrorCodes.InvalidToken, expired.ErrorCode);
        Assert.Equal(UserInvitationStatus.Expired, (await WithServiceAsync(service => service.ListAsync(administrator))).Single().Status);

        var renewed = await WithServiceAsync(service => service.RenewLinkAsync(created.Invitation.Id, administrator));
        Assert.True(renewed.Succeeded);
        Assert.False(renewed.Value!.EmailSent);
        Assert.Equal(UserInvitationStatus.Pending, renewed.Value.Invitation.Status);

        var accepted = await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(renewed.Value))));
        Assert.True(accepted.Succeeded, accepted.ErrorMessage);
    }

    [Fact]
    public async Task Resend_and_renew_invalidate_the_previous_link()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);

        var resent = await WithServiceAsync(service => service.ResendAsync(created.Invitation.Id, administrator));

        Assert.True(resent.Succeeded);
        Assert.True(resent.Value!.EmailSent);
        Assert.Equal(2, resent.Value.Invitation.SendCount);
        Assert.Equal(2, EmailSender.Emails.Count);
        Assert.NotEqual(created.Link, resent.Value.Link);
        Assert.False((await WithServiceAsync(service => service.PreviewAsync(TokenOf(created)))).IsValid);

        var renewed = await WithServiceAsync(service => service.RenewLinkAsync(created.Invitation.Id, administrator));

        Assert.Equal(2, EmailSender.Emails.Count);
        Assert.False((await WithServiceAsync(service => service.PreviewAsync(TokenOf(resent.Value)))).IsValid);
        Assert.True((await WithServiceAsync(service => service.PreviewAsync(TokenOf(renewed.Value!)))).IsValid);
    }

    [Fact]
    public async Task A_revoked_invitation_cannot_be_accepted_resent_or_renewed()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);

        var revoked = await WithServiceAsync(service => service.RevokeAsync(created.Invitation.Id, administrator));
        var revokedAgain = await WithServiceAsync(service => service.RevokeAsync(created.Invitation.Id, administrator));

        Assert.True(revoked.Succeeded);
        Assert.True(revokedAgain.Succeeded);
        Assert.Equal(UserInvitationErrorCodes.InvalidToken, (await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(created))))).ErrorCode);
        Assert.Equal(UserInvitationOutcome.Conflict, (await WithServiceAsync(service => service.ResendAsync(created.Invitation.Id, administrator))).Outcome);
        Assert.Equal(UserInvitationOutcome.Conflict, (await WithServiceAsync(service => service.RenewLinkAsync(created.Invitation.Id, administrator))).Outcome);
        Assert.Equal(UserInvitationOutcome.NotFound, (await WithServiceAsync(service => service.RevokeAsync(Guid.NewGuid(), administrator))).Outcome);
    }

    [Fact]
    public async Task An_accepted_invitation_cannot_be_revoked()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);
        await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(created))));

        var result = await WithServiceAsync(service => service.RevokeAsync(created.Invitation.Id, administrator));

        Assert.Equal(UserInvitationOutcome.Conflict, result.Outcome);
        Assert.Equal(UserInvitationErrorCodes.NotPending, result.ErrorCode);
    }

    [Fact]
    public async Task A_weak_password_is_refused_and_the_link_stays_usable()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);

        var refused = await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(created), password: "short")));

        Assert.Equal(UserInvitationErrorCodes.PasswordRejected, refused.ErrorCode);
        Assert.NotEmpty(refused.Errors![nameof(AcceptUserInvitationRequest.Password)]);
        Assert.True((await WithServiceAsync(service => service.PreviewAsync(TokenOf(created)))).IsValid);

        using var scope = CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>().FindByEmailAsync(InvitedEmail));
    }

    [Fact]
    public async Task A_refusing_handler_rolls_the_account_back()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);
        Handler.AcceptedRejection = "The household no longer exists.";

        var refused = await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(created))));

        Assert.Equal(UserInvitationOutcome.Rejected, refused.Outcome);
        Assert.Equal("The household no longer exists.", refused.ErrorMessage);

        using var scope = CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>().FindByEmailAsync(InvitedEmail));
        Assert.True((await WithServiceAsync(service => service.PreviewAsync(TokenOf(created)))).IsValid);
    }

    [Fact]
    public async Task Accepting_when_the_address_got_an_account_meanwhile_is_a_conflict()
    {
        var (_, administrator) = await SeedAdministratorAsync();
        var created = await CreateInvitationAsync(administrator);
        await SeedUserAsync(InvitedEmail, "Created meanwhile");

        var result = await WithServiceAsync(service => service.AcceptAsync(AcceptRequest(TokenOf(created))));

        Assert.Equal(UserInvitationOutcome.Conflict, result.Outcome);
        Assert.Equal(UserInvitationErrorCodes.UserExists, result.ErrorCode);
    }

    [Fact]
    public void Token_generator_creates_distinct_tokens_and_matches_only_its_own_hash()
    {
        var token = UserInvitationTokenGenerator.CreateToken();
        var otherToken = UserInvitationTokenGenerator.CreateToken();

        Assert.NotEqual(token, otherToken);
        Assert.True(UserInvitationTokenGenerator.MatchesHash(token, UserInvitationTokenGenerator.Hash(token)));
        Assert.False(UserInvitationTokenGenerator.MatchesHash(otherToken, UserInvitationTokenGenerator.Hash(token)));
    }
}
