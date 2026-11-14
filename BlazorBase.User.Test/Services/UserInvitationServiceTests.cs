using System.Net;
using System.Text;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// The invitation client calls the configured path and turns a problem details answer with a <c>code</c>
/// into <see cref="UserInvitationException"/>, while other failures stay plain <see cref="HttpRequestException"/>s.
/// </summary>
public sealed class UserInvitationServiceTests
{
    private sealed class StubHandler(HttpStatusCode statusCode, string body, string mediaType) : HttpMessageHandler
    {
        #region Injects
        private readonly HttpStatusCode StatusCode = statusCode;
        private readonly string Body = body;
        private readonly string MediaType = mediaType;
        #endregion

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(StatusCode) { Content = new StringContent(Body, Encoding.UTF8, MediaType) });
        }
    }

    private static (UserInvitationService Service, StubHandler Handler) Create(HttpStatusCode statusCode, string body, string mediaType = "application/json")
    {
        var handler = new StubHandler(statusCode, body, mediaType);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var options = Options.Create(new BlazorBaseUserClientOptions { InvitationEndpointBasePath = "/api/custom/invitations/" });
        return (new UserInvitationService(httpClient, options), handler);
    }

    [Fact]
    public async Task Preview_calls_the_configured_path_with_the_escaped_token()
    {
        var (service, handler) = Create(HttpStatusCode.OK, "{\"isValid\":true,\"email\":\"a@example.com\"}");

        var preview = await service.GetPreviewAsync("a+b/c");

        Assert.True(preview.IsValid);
        Assert.Equal("https://localhost/api/custom/invitations/accept?token=a%2Bb%2Fc", handler.Requests.Single().RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task A_problem_with_a_code_becomes_a_user_invitation_exception()
    {
        const string problem = "{\"title\":\"One or more validation errors occurred.\",\"status\":400,\"detail\":\"weak\",\"code\":\"password_rejected\",\"errors\":{\"Password\":[\"Too short.\",\"No digit.\"]}}";
        var (service, _) = Create(HttpStatusCode.BadRequest, problem, "application/problem+json");

        var exception = await Assert.ThrowsAsync<UserInvitationException>(() => service.AcceptAsync(new AcceptUserInvitationRequest()));

        Assert.Equal(UserInvitationErrorCodes.PasswordRejected, exception.ErrorCode);
        Assert.Equal("weak", exception.Message);
        Assert.Equal(["Too short.", "No digit."], exception.Errors);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task A_failure_without_a_code_stays_a_plain_http_request_exception()
    {
        var (service, _) = Create(HttpStatusCode.Forbidden, string.Empty, "text/plain");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetInvitationsAsync());

        Assert.IsNotType<UserInvitationException>(exception);
        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
    }
}
