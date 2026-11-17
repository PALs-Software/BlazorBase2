using BlazorBase.Components.Services;
using BlazorBase.User.Models;
using BlazorBase.User.Pages;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Components;

/// <summary>
/// The invitation administration lists invitations with their status, offers resend, new link and revoke
/// only for open or expired ones, shows a new link with a warning when no email went out, and revokes only
/// after confirmation.
/// </summary>
public class InvitationManagementViewTests : EnglishUserBunitTestContextBase
{
    private static readonly UserInvitationSummary PendingInvitation = new()
    {
        Id = Guid.NewGuid(),
        Email = "pending@example.com",
        DisplayName = "Pending Person",
        Role = "User",
        Status = UserInvitationStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
    };

    private static readonly UserInvitationSummary AcceptedInvitation = new()
    {
        Id = Guid.NewGuid(),
        Email = "accepted@example.com",
        Role = "Admin",
        Status = UserInvitationStatus.Accepted,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        AcceptedAt = DateTime.UtcNow,
    };

    private readonly IUserInvitationService InvitationService = Substitute.For<IUserInvitationService>();
    private readonly IConfirmationService ConfirmationService = Substitute.For<IConfirmationService>();

    public InvitationManagementViewTests()
    {
        Services.AddLocalization();
        InvitationService.GetInvitationsAsync().Returns(new List<UserInvitationSummary> { PendingInvitation, AcceptedInvitation });
        Services.AddSingleton(InvitationService);
        Services.AddSingleton(ConfirmationService);
        Services.AddSingleton<IUserRoleProvider>(new StubUserRoleProvider(["Admin", "User"]));
    }

    [Fact]
    public void Lists_invitations_with_status_and_actions_only_for_open_ones()
    {
        var cut = Render<InvitationManagementView>();

        var items = cut.FindAll(".invitation-management-item");
        Assert.Equal(2, items.Count);
        Assert.Contains("pending@example.com", items[0].TextContent);
        Assert.Contains("Pending", items[0].QuerySelector(".invitation-management-status-pending")!.TextContent);
        Assert.NotNull(items[0].QuerySelector(".invitation-management-actions"));
        Assert.Contains("Accepted", items[1].QuerySelector(".invitation-management-status-accepted")!.TextContent);
        Assert.Null(items[1].QuerySelector(".invitation-management-actions"));
    }

    [Fact]
    public void A_new_link_without_email_is_shown_with_a_warning()
    {
        InvitationService.RenewLinkAsync(PendingInvitation.Id).Returns(new UserInvitationLinkResponse
        {
            Invitation = PendingInvitation,
            Link = "/invitation/accept?token=fresh",
            EmailSent = false,
        });

        var cut = Render<InvitationManagementView>();
        cut.Find(".invitation-management-new-link").Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/invitation/accept?token=fresh", cut.Find(".invitation-management-link-value").TextContent));
        Assert.StartsWith("http", cut.Find(".invitation-management-link-value").TextContent);
        Assert.Contains("No email was sent", cut.Find(".invitation-management-link").TextContent);
    }

    [Fact]
    public void Resend_reports_the_sent_email()
    {
        InvitationService.ResendAsync(PendingInvitation.Id).Returns(new UserInvitationLinkResponse
        {
            Invitation = PendingInvitation,
            Link = "https://app.example.com/invitation/accept?token=again",
            EmailSent = true,
        });

        var cut = Render<InvitationManagementView>();
        cut.Find(".invitation-management-resend").Click();

        cut.WaitForAssertion(() => Assert.Contains("pending@example.com", cut.Find(".invitation-management-link").TextContent));
        Assert.Equal("https://app.example.com/invitation/accept?token=again", cut.Find(".invitation-management-link-value").TextContent);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void Revokes_only_after_confirmation(bool confirms, int expectedRevocations)
    {
        ConfirmationService.ConfirmDestructiveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(confirms);

        var cut = Render<InvitationManagementView>();
        cut.Find(".invitation-management-revoke").Click();

        cut.WaitForAssertion(() => InvitationService.Received(expectedRevocations).RevokeAsync(PendingInvitation.Id));
    }
}
