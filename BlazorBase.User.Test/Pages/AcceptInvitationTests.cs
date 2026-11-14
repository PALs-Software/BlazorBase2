using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.Pages;
using BlazorBase.User.Services;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Pages;

/// <summary>
/// The accept page shows the invited address for a valid token, one message for every unusable token, and
/// after a successful accept signs the new account in and leaves for the start page. Refusals of the server
/// (a rejected password, a link used meanwhile) are shown on the page.
/// </summary>
public class AcceptInvitationTests : AccountPageTestContextBase
{
    private const string Token = "token-value";

    private readonly IUserInvitationService InvitationService = Substitute.For<IUserInvitationService>();

    public AcceptInvitationTests() => Services.AddSingleton(InvitationService);

    private IRenderedComponent<AcceptInvitation> RenderPage(string query = $"?token={Token}")
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/invitation/accept{query}");
        return Render<AcceptInvitation>();
    }

    private void ReturnValidPreview() =>
        InvitationService.GetPreviewAsync(Token).Returns(new UserInvitationPreview
        {
            IsValid = true,
            Email = "new.person@example.com",
            DisplayName = "New Person",
            ExpiresAt = DateTime.UtcNow.AddDays(3),
        });

    private static void FillPasswords(IRenderedComponent<AcceptInvitation> cut, string password, string confirmation)
    {
        var fields = cut.FindComponents<FluentTextInput>();
        cut.InvokeAsync(() => fields[1].Instance.ValueChanged.InvokeAsync(password));
        cut.InvokeAsync(() => fields[2].Instance.ValueChanged.InvokeAsync(confirmation));
    }

    [Fact]
    public void Shows_the_invited_address_and_prefills_the_display_name()
    {
        ReturnValidPreview();

        var cut = RenderPage();

        Assert.Equal("new.person@example.com", cut.Find(".accept-invitation-email").TextContent);
        var fields = cut.FindComponents<FluentTextInput>();
        Assert.Equal(3, fields.Count);
        Assert.Equal("New Person", fields[0].Instance.Value);
        Assert.Equal("new-password", fields[1].Instance.AutoComplete);
    }

    [Fact]
    public void An_unusable_token_shows_the_invalid_link_message_without_a_form()
    {
        InvitationService.GetPreviewAsync(Token).Returns(new UserInvitationPreview());

        var cut = RenderPage();

        Assert.Contains("invalid", cut.Find(".accept-invitation-status").TextContent);
        Assert.Empty(cut.FindComponents<FluentTextInput>());
    }

    [Fact]
    public void A_missing_token_does_not_call_the_server()
    {
        var cut = RenderPage(query: string.Empty);

        Assert.Contains("invalid", cut.Find(".accept-invitation-status").TextContent);
        InvitationService.DidNotReceiveWithAnyArgs().GetPreviewAsync(default!);
    }

    [Fact]
    public void Accepting_signs_in_and_leaves_for_the_start_page()
    {
        ReturnValidPreview();
        var tokens = new LoginResponse { AccessToken = "access", RefreshToken = "refresh", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        InvitationService.AcceptAsync(Arg.Any<AcceptUserInvitationRequest>()).Returns(tokens);

        var cut = RenderPage();
        FillPasswords(cut, "Secret123456", "Secret123456");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.EndsWith("/", Services.GetRequiredService<NavigationManager>().Uri));
        InvitationService.Received(1).AcceptAsync(Arg.Is<AcceptUserInvitationRequest>(request =>
            request.Token == Token && request.DisplayName == "New Person" && request.Password == "Secret123456"));
        TokenStorage.Received(1).SaveTokensAsync(tokens);
    }

    [Fact]
    public void Different_passwords_are_refused_before_the_server_is_asked()
    {
        ReturnValidPreview();

        var cut = RenderPage();
        FillPasswords(cut, "Secret123456", "Different123456");
        cut.Find("form").Submit();

        Assert.Contains("do not match", cut.Find(".accept-invitation-error").TextContent);
        InvitationService.DidNotReceiveWithAnyArgs().AcceptAsync(default!);
    }

    [Fact]
    public void A_rejected_password_shows_the_server_reasons()
    {
        ReturnValidPreview();
        InvitationService.AcceptAsync(Arg.Any<AcceptUserInvitationRequest>())
            .Returns<LoginResponse>(_ => throw new UserInvitationException(UserInvitationErrorCodes.PasswordRejected, "weak", ["Passwords must have at least one digit."], HttpStatusCode.BadRequest));

        var cut = RenderPage();
        FillPasswords(cut, "Secretpassword", "Secretpassword");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("at least one digit", cut.Find(".accept-invitation-error").TextContent));
    }

    [Fact]
    public void A_link_used_meanwhile_replaces_the_form_with_the_invalid_link_message()
    {
        ReturnValidPreview();
        InvitationService.AcceptAsync(Arg.Any<AcceptUserInvitationRequest>())
            .Returns<LoginResponse>(_ => throw new UserInvitationException(UserInvitationErrorCodes.InvalidToken, "invalid", [], HttpStatusCode.BadRequest));

        var cut = RenderPage();
        FillPasswords(cut, "Secret123456", "Secret123456");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("invalid", cut.Find(".accept-invitation-status").TextContent));
    }
}
