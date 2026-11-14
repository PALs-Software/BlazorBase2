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
/// <c>/forgot-password</c> requests the link and confirms neutrally; <c>/reset-password</c> reads email and
/// token from the link the account endpoints build, submits the new password and shows the server's reason
/// for a refusal; the login page links to the first one only when the application opts in.
/// </summary>
public class PasswordResetPagesTests : AccountPageTestContextBase
{
    private IRenderedComponent<TPage> RenderAt<TPage>(string uri)
        where TPage : IComponent
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
        return Render<TPage>();
    }

    [Fact]
    public void Forgot_password_requests_the_link_and_confirms()
    {
        var cut = RenderAt<ForgotPassword>("/forgot-password");

        var emailField = cut.FindComponent<FluentTextInput>();
        cut.InvokeAsync(() => emailField.Instance.ValueChanged.InvokeAsync("someone@example.com"));
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".forgot-password-sent")));
        AuthService.Received(1).ForgotPasswordAsync(Arg.Is<ForgotPasswordRequest>(request => request.Email == "someone@example.com"));
    }

    [Fact]
    public void Reset_password_without_link_parameters_shows_the_invalid_link_message()
    {
        var cut = RenderAt<ResetPassword>("/reset-password");

        Assert.NotNull(cut.Find(".reset-password-invalid-link"));
        Assert.Empty(cut.FindComponents<FluentTextInput>());
    }

    [Fact]
    public void Reset_password_submits_email_token_and_new_password()
    {
        var cut = RenderAt<ResetPassword>("/reset-password?email=someone%40example.com&token=abc%2Bdef%3D");

        Assert.Equal("someone@example.com", cut.Find(".reset-password-email").TextContent);

        var fields = cut.FindComponents<FluentTextInput>();
        cut.InvokeAsync(() => fields[0].Instance.ValueChanged.InvokeAsync("Secret123456"));
        cut.InvokeAsync(() => fields[1].Instance.ValueChanged.InvokeAsync("Secret123456"));
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".reset-password-completed")));
        AuthService.Received(1).ResetPasswordAsync(Arg.Is<ResetPasswordRequest>(request =>
            request.Email == "someone@example.com" && request.Token == "abc+def=" && request.NewPassword == "Secret123456"));
    }

    [Fact]
    public void Reset_password_shows_the_reason_of_a_refusal()
    {
        AuthService.ResetPasswordAsync(Arg.Any<ResetPasswordRequest>())
            .Returns(_ => throw new HttpRequestException("Invalid or expired password reset link.", null, HttpStatusCode.BadRequest));

        var cut = RenderAt<ResetPassword>("/reset-password?email=someone%40example.com&token=abc");
        var fields = cut.FindComponents<FluentTextInput>();
        cut.InvokeAsync(() => fields[0].Instance.ValueChanged.InvokeAsync("Secret123456"));
        cut.InvokeAsync(() => fields[1].Instance.ValueChanged.InvokeAsync("Secret123456"));
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("expired", cut.Find(".reset-password-error").TextContent));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Login_links_to_forgot_password_only_when_enabled(bool enabled)
    {
        AuthService.GetStatusAsync().Returns(new AuthStatusResponse { HasUsers = true });
        Services.Configure<BlazorBaseUserClientOptions>(options => options.ShowForgotPasswordLink = enabled);

        var cut = Render<Login>();

        Assert.Equal(enabled ? 1 : 0, cut.FindAll(".login-forgot-password").Count);
    }
}
