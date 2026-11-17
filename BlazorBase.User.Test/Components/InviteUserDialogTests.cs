using System.Net;
using BlazorBase.Components.Test.Infrastructure;
using BlazorBase.User.Components.Invitations;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Components;

/// <summary>
/// The invite dialog sends email, display name, role, language and the context the application's fields
/// set, closes with the server's link response, and keeps a refusal inside the dialog.
/// </summary>
public class InviteUserDialogTests : EnglishUserBunitTestContextBase
{
    private readonly IUserInvitationService InvitationService = Substitute.For<IUserInvitationService>();

    public InviteUserDialogTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(InvitationService);
    }

    private static readonly RenderFragment<CreateUserInvitationRequest> ContextFields = request => builder =>
    {
        request.Context = "household-7";
        builder.AddMarkupContent(0, "<span class=\"app-context-field\">Household 7</span>");
    };

    private DialogHarness<InviteUserDialog> OpenDialog() =>
        DialogHarness<InviteUserDialog>.Open(this, "Invite", new InviteUserDialogContent
        {
            Roles = ["Admin", "User"],
            DefaultRole = "User",
            Language = "de",
            ContextContent = ContextFields,
        });

    private static void FillEmail(DialogHarness<InviteUserDialog> dialog, string email)
    {
        var emailField = dialog.Content.FindComponents<FluentTextInput>()[0];
        dialog.Content.InvokeAsync(() => emailField.Instance.ValueChanged.InvokeAsync(email));
    }

    [Fact]
    public async Task Creates_the_invitation_with_the_application_context_and_closes_with_the_link()
    {
        var response = new UserInvitationLinkResponse { Link = "https://app.example.com/invitation/accept?token=abc", EmailSent = true };
        InvitationService.CreateAsync(Arg.Any<CreateUserInvitationRequest>()).Returns(response);

        var dialog = OpenDialog();
        Assert.NotNull(dialog.Content.Find(".app-context-field"));

        FillEmail(dialog, "new.person@example.com");
        dialog.Content.Find(".invite-user-dialog-submit").Click();

        var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Cancelled);
        Assert.Same(response, result.Value);
        await InvitationService.Received(1).CreateAsync(Arg.Is<CreateUserInvitationRequest>(request =>
            request.Email == "new.person@example.com" && request.Role == "User" && request.Language == "de" && request.Context == "household-7"));
    }

    [Fact]
    public void An_existing_account_is_reported_inside_the_dialog()
    {
        InvitationService.CreateAsync(Arg.Any<CreateUserInvitationRequest>())
            .Returns<UserInvitationLinkResponse>(_ => throw new UserInvitationException(UserInvitationErrorCodes.UserExists, "exists", [], HttpStatusCode.Conflict));

        var dialog = OpenDialog();
        FillEmail(dialog, "existing@example.com");
        dialog.Content.Find(".invite-user-dialog-submit").Click();

        dialog.Content.WaitForAssertion(() => Assert.Contains("already exists", dialog.Content.Find(".invite-user-dialog-error").TextContent));
        Assert.False(dialog.Result.IsCompleted);
    }

    [Fact]
    public void An_invalid_address_is_not_sent()
    {
        var dialog = OpenDialog();
        FillEmail(dialog, "not-an-address");
        dialog.Content.Find(".invite-user-dialog-submit").Click();

        InvitationService.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }
}
