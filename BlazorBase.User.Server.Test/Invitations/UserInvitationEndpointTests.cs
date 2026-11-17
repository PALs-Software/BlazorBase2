using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Invitations.Endpoints;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.Invitations;

/// <summary>
/// The invitation API through a real <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>: administration is
/// limited to the admin role, the invitation email is rendered from the standard template with an encoded link,
/// an existing account answers 409, the anonymous preview answers an unknown and a revoked token identically,
/// and accepting signs the new account in.
/// </summary>
public sealed class UserInvitationEndpointTests
{
    private static async Task<UserInvitationLinkResponse> CreateInvitationAsync(HttpClient administrator, string email = "new.person@example.com")
    {
        var response = await administrator.PostAsJsonAsync(InvitationTestHost.BasePath, new CreateUserInvitationRequest { Email = email, DisplayName = "New Person", Role = "User", Language = "en" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserInvitationLinkResponse>())!;
    }

    private static string TokenOf(UserInvitationLinkResponse response) =>
        Uri.UnescapeDataString(response.Link[(response.Link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    [Fact]
    public async Task Administration_requires_the_admin_role()
    {
        await using var host = await InvitationTestHost.StartAsync();
        var anonymous = host.CreateClient();
        var user = await host.CreateClientForRoleAsync("User", "user@example.com");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(InvitationTestHost.BasePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(InvitationTestHost.BasePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync(InvitationTestHost.BasePath, new CreateUserInvitationRequest { Email = "x@example.com", Role = "Admin" })).StatusCode);
    }

    [Fact]
    public async Task Create_sends_the_standard_email_with_the_encoded_link_and_lists_the_invitation()
    {
        await using var host = await InvitationTestHost.StartAsync();
        var administrator = await host.CreateClientForRoleAsync("Admin");

        var created = await CreateInvitationAsync(administrator);

        Assert.True(created.EmailSent);
        Assert.StartsWith("https://app.example.com/invitation/accept?token=", created.Link);

        var message = Assert.Single(host.SentEmails.Messages);
        Assert.Equal(["new.person@example.com"], message.To);
        Assert.Equal("You are invited to TestBrand", message.Subject);
        Assert.Contains($"href=\"{created.Link}\"", message.HtmlBody);
        Assert.Contains("Admin Person has invited you to join TestBrand.", message.HtmlBody);

        var invitations = await administrator.GetFromJsonAsync<List<UserInvitationSummary>>(InvitationTestHost.BasePath);
        var invitation = Assert.Single(invitations!);
        Assert.Equal(UserInvitationStatus.Pending, invitation.Status);
        Assert.Equal(1, invitation.SendCount);
    }

    [Fact]
    public async Task Create_without_mailing_reports_that_no_email_was_sent()
    {
        await using var host = await InvitationTestHost.StartAsync(withMailing: false);
        var administrator = await host.CreateClientForRoleAsync("Admin");

        var created = await CreateInvitationAsync(administrator);

        Assert.False(created.EmailSent);
        Assert.NotEmpty(created.Link);
    }

    [Fact]
    public async Task Create_for_an_existing_account_answers_409()
    {
        await using var host = await InvitationTestHost.StartAsync();
        var administrator = await host.CreateClientForRoleAsync("Admin");

        var response = await administrator.PostAsJsonAsync(InvitationTestHost.BasePath, new CreateUserInvitationRequest { Email = "ADMIN@example.com", Role = "User" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(UserInvitationErrorCodes.UserExists, await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Preview_answers_an_unknown_and_a_revoked_token_identically()
    {
        await using var host = await InvitationTestHost.StartAsync();
        var administrator = await host.CreateClientForRoleAsync("Admin");
        var anonymous = host.CreateClient();
        var created = await CreateInvitationAsync(administrator);

        var validBody = await anonymous.GetStringAsync($"{InvitationTestHost.BasePath}/accept?token={Uri.EscapeDataString(TokenOf(created))}");
        Assert.Contains("new.person@example.com", validBody);

        Assert.Equal(HttpStatusCode.NoContent, (await administrator.DeleteAsync($"{InvitationTestHost.BasePath}/{created.Invitation.Id}")).StatusCode);

        var revokedBody = await anonymous.GetStringAsync($"{InvitationTestHost.BasePath}/accept?token={Uri.EscapeDataString(TokenOf(created))}");
        var unknownBody = await anonymous.GetStringAsync($"{InvitationTestHost.BasePath}/accept?token=unknown");
        var missingBody = await anonymous.GetStringAsync($"{InvitationTestHost.BasePath}/accept");

        Assert.Equal(unknownBody, revokedBody);
        Assert.Equal(unknownBody, missingBody);
        Assert.DoesNotContain("new.person", revokedBody);
        Assert.False(JsonSerializer.Deserialize<UserInvitationPreview>(unknownBody, JsonSerializerOptions.Web)!.IsValid);
    }

    [Fact]
    public async Task Accept_signs_the_new_account_in_and_the_link_then_stops_working()
    {
        await using var host = await InvitationTestHost.StartAsync();
        var administrator = await host.CreateClientForRoleAsync("Admin");
        var anonymous = host.CreateClient();
        var created = await CreateInvitationAsync(administrator);
        var request = new AcceptUserInvitationRequest { Token = TokenOf(created), DisplayName = "Chosen Name", Password = "Secret123456" };

        var accepted = await anonymous.PostAsJsonAsync($"{InvitationTestHost.BasePath}/accept", request);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var tokens = await accepted.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(tokens!.AccessToken));
        Assert.Equal("no-store", accepted.Headers.CacheControl?.ToString());

        var again = await anonymous.PostAsJsonAsync($"{InvitationTestHost.BasePath}/accept", request);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(UserInvitationErrorCodes.InvalidToken, await ReadErrorCodeAsync(again));
    }

    [Fact]
    public async Task Accept_with_a_weak_password_answers_a_validation_problem()
    {
        await using var host = await InvitationTestHost.StartAsync();
        var administrator = await host.CreateClientForRoleAsync("Admin");
        var created = await CreateInvitationAsync(administrator);

        var response = await host.CreateClient().PostAsJsonAsync($"{InvitationTestHost.BasePath}/accept",
            new AcceptUserInvitationRequest { Token = TokenOf(created), DisplayName = "Chosen Name", Password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(UserInvitationErrorCodes.PasswordRejected, document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.GetProperty("errors").GetProperty("Password").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Resend_and_link_answer_404_for_an_unknown_invitation()
    {
        await using var host = await InvitationTestHost.StartAsync();
        var administrator = await host.CreateClientForRoleAsync("Admin");

        var resend = await administrator.PostAsync($"{InvitationTestHost.BasePath}/{Guid.NewGuid()}/resend", content: null);
        var link = await administrator.PostAsync($"{InvitationTestHost.BasePath}/{Guid.NewGuid()}/link", content: null);

        Assert.Equal(HttpStatusCode.NotFound, resend.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, link.StatusCode);
    }

    [Fact]
    public void Mapping_without_registration_fails_with_a_clear_message()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapBlazorBaseUserInvitationEndpoints<TestUser>());

        Assert.Contains("AddBlazorBaseUserInvitations", exception.Message);
    }
}
