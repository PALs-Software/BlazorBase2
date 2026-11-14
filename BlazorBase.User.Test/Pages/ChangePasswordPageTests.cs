using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.Pages;
using BlazorBase.User.Test.Infrastructure;
using Bunit;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Pages;

/// <summary>
/// <c>/account/password</c> reports a missing field and a mismatching confirmation next to the field without
/// calling the server, submits the current and the new password, replaces the form by a confirmation on
/// success, maps the server's refusals back to the fields and shows a failure that belongs to no field above
/// the button. The show-password toggles switch each field between a password and a text input.
/// </summary>
public class ChangePasswordPageTests : AccountPageTestContextBase
{
    private static async Task EnterAsync(IRenderedComponent<ChangePassword> cut, string currentPassword, string newPassword, string confirmation)
    {
        var fields = cut.FindComponents<FluentTextInput>();
        await cut.InvokeAsync(() => fields[0].Instance.ValueChanged.InvokeAsync(currentPassword));
        await cut.InvokeAsync(() => fields[1].Instance.ValueChanged.InvokeAsync(newPassword));
        await cut.InvokeAsync(() => fields[2].Instance.ValueChanged.InvokeAsync(confirmation));
    }

    [Fact]
    public void Renders_three_password_fields_with_a_toggle_each()
    {
        var cut = Render<ChangePassword>();

        Assert.Equal(3, cut.FindComponents<FluentTextInput>().Count);
        Assert.All(cut.FindComponents<FluentTextInput>(), field => Assert.Equal(TextInputType.Password, field.Instance.TextInputType));
        Assert.Equal(3, cut.FindAll(".change-password-toggle").Count);
        Assert.Equal("Change password", cut.Find("h2").TextContent);
    }

    [Fact]
    public void Submitting_an_empty_form_reports_the_missing_fields_without_calling_the_server()
    {
        var cut = Render<ChangePassword>();

        cut.Find("form").Submit();

        Assert.Equal("Enter your current password.", cut.Find(".change-password-current-error").TextContent);
        Assert.Equal("Enter a new password.", cut.Find(".change-password-new-error").TextContent);
        _ = AuthService.DidNotReceive().ChangePasswordAsync(Arg.Any<ChangePasswordRequest>());
    }

    [Fact]
    public async Task A_confirmation_that_differs_is_reported_while_typing_and_blocks_the_submit()
    {
        var cut = Render<ChangePassword>();

        await EnterAsync(cut, "Old-Secret-1", "New-Secret-1", "New-Secret-2");

        Assert.Equal("Passwords do not match.", cut.Find(".change-password-confirmation-error").TextContent);

        cut.Find("form").Submit();

        _ = AuthService.DidNotReceive().ChangePasswordAsync(Arg.Any<ChangePasswordRequest>());
    }

    [Fact]
    public async Task A_matching_confirmation_shows_no_error()
    {
        var cut = Render<ChangePassword>();

        await EnterAsync(cut, "Old-Secret-1", "New-Secret-1", "New-Secret-1");

        Assert.Empty(cut.FindAll(".change-password-confirmation-error"));
    }

    [Fact]
    public async Task Submitting_valid_values_sends_both_passwords_and_shows_the_success_state()
    {
        AuthService.ChangePasswordAsync(Arg.Any<ChangePasswordRequest>()).Returns(ChangePasswordOutcome.Success());
        var cut = Render<ChangePassword>();

        await EnterAsync(cut, "Old-Secret-1", "New-Secret-1", "New-Secret-1");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".change-password-completed")));
        await AuthService.Received(1).ChangePasswordAsync(Arg.Is<ChangePasswordRequest>(request =>
            request.CurrentPassword == "Old-Secret-1" && request.NewPassword == "New-Secret-1"));
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public async Task A_wrong_current_password_is_shown_at_the_current_password_field()
    {
        AuthService.ChangePasswordAsync(Arg.Any<ChangePasswordRequest>())
            .Returns(ChangePasswordOutcome.Rejected(ChangePasswordErrorCodes.CurrentPasswordIncorrect, "The current password is incorrect."));
        var cut = Render<ChangePassword>();

        await EnterAsync(cut, "Wrong-Secret-1", "New-Secret-1", "New-Secret-1");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("The current password is incorrect.", cut.Find(".change-password-current-error").TextContent));
        Assert.Empty(cut.FindAll(".change-password-new-error"));
        Assert.Empty(cut.FindAll(".change-password-completed"));
    }

    [Fact]
    public async Task The_reasons_a_new_password_is_rejected_are_shown_at_the_new_password_field()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["newPassword"] = ["Passwords must be at least 8 characters.", "Passwords must have at least one digit ('0'-'9')."],
        };
        AuthService.ChangePasswordAsync(Arg.Any<ChangePasswordRequest>())
            .Returns(ChangePasswordOutcome.Rejected(ChangePasswordErrorCodes.ValidationFailed, "The new password was rejected.", fieldErrors));
        var cut = Render<ChangePassword>();

        await EnterAsync(cut, "Old-Secret-1", "weak", "weak");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".change-password-new-error").Count));
        Assert.Contains("at least 8 characters", cut.FindAll(".change-password-new-error")[0].TextContent);
        Assert.Empty(cut.FindAll(".change-password-current-error"));
    }

    [Fact]
    public async Task An_account_without_a_password_gets_a_message_above_the_button()
    {
        AuthService.ChangePasswordAsync(Arg.Any<ChangePasswordRequest>())
            .Returns(ChangePasswordOutcome.Rejected(ChangePasswordErrorCodes.NoPasswordSet, "This account has no password."));
        var cut = Render<ChangePassword>();

        await EnterAsync(cut, "Old-Secret-1", "New-Secret-1", "New-Secret-1");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("no password yet", cut.Find(".change-password-error").TextContent));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "session has expired")]
    [InlineData(HttpStatusCode.TooManyRequests, "Too many attempts")]
    [InlineData(HttpStatusCode.InternalServerError, "could not be changed")]
    public async Task A_failed_request_shows_a_message_that_fits_the_status(HttpStatusCode statusCode, string expectedText)
    {
        AuthService.ChangePasswordAsync(Arg.Any<ChangePasswordRequest>())
            .Returns<ChangePasswordOutcome>(_ => throw new HttpRequestException("failed", null, statusCode));
        var cut = Render<ChangePassword>();

        await EnterAsync(cut, "Old-Secret-1", "New-Secret-1", "New-Secret-1");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains(expectedText, cut.Find(".change-password-error").TextContent));
    }

    [Fact]
    public void Each_toggle_switches_its_own_field_between_password_and_text()
    {
        var cut = Render<ChangePassword>();

        cut.Find(".change-password-toggle-new").Click();

        var fields = cut.FindComponents<FluentTextInput>();
        Assert.Equal(TextInputType.Password, fields[0].Instance.TextInputType);
        Assert.Equal(TextInputType.Text, fields[1].Instance.TextInputType);
        Assert.Equal(TextInputType.Password, fields[2].Instance.TextInputType);
        Assert.Equal("true", cut.Find(".change-password-toggle-new").GetAttribute("aria-pressed"));
        Assert.Equal("Hide password", cut.Find(".change-password-toggle-new").GetAttribute("aria-label"));

        cut.Find(".change-password-toggle-new").Click();

        Assert.Equal(TextInputType.Password, cut.FindComponents<FluentTextInput>()[1].Instance.TextInputType);
    }
}
