using System.Net;
using System.Net.Http.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// <see cref="AuthService.ChangePasswordAsync"/> posts to <c>api/auth/manage/change-password</c>, attaches the
/// stored access token (bearer transport only), adopts the replacement token pair of a success, turns the
/// server's explained <c>400</c> bodies into a <see cref="ChangePasswordOutcome"/> and throws for every other
/// failure.
/// </summary>
public sealed class AuthServiceChangePasswordTests
{
    private static readonly ChangePasswordRequest Request = new() { CurrentPassword = "Old-Secret-1", NewPassword = "New-Secret-1" };

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        #region Injects
        private readonly HttpResponseMessage Response = response;
        #endregion

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }

    private static AuthService CreateAuthService(HttpResponseMessage response, out RecordingHandler handler, out ITokenStorage tokenStorage, LoginResponse? storedTokens = null)
    {
        handler = new RecordingHandler(response);
        tokenStorage = Substitute.For<ITokenStorage>();
        tokenStorage.GetTokensAsync().Returns(Task.FromResult(storedTokens));
        return new AuthService(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") }, tokenStorage);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Success_posts_both_passwords_to_the_change_password_endpoint()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ChangePasswordResponse()) };
        var authService = CreateAuthService(response, out var handler, out _);

        var outcome = await authService.ChangePasswordAsync(Request);

        Assert.True(outcome.Succeeded);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://localhost/api/auth/manage/change-password", handler.LastRequest.RequestUri!.ToString());
        var sent = System.Text.Json.JsonSerializer.Deserialize<ChangePasswordRequest>(handler.LastBody!, System.Text.Json.JsonSerializerOptions.Web);
        Assert.Equal("Old-Secret-1", sent!.CurrentPassword);
        Assert.Equal("New-Secret-1", sent.NewPassword);
    }

    [Fact]
    public async Task Success_stores_the_replacement_token_pair()
    {
        var refreshed = new LoginResponse { AccessToken = "new-access", RefreshToken = "new-refresh", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) };
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ChangePasswordResponse { RefreshedTokens = refreshed }) };
        var authService = CreateAuthService(response, out _, out var tokenStorage);

        await authService.ChangePasswordAsync(Request);

        await tokenStorage.Received(1).SaveTokensAsync(Arg.Is<LoginResponse>(tokens => tokens.AccessToken == "new-access"));
    }

    [Fact]
    public async Task Success_under_the_cookie_transport_does_not_touch_the_token_storage()
    {
        var blanked = new LoginResponse { AccessToken = string.Empty, RefreshToken = string.Empty, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) };
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ChangePasswordResponse { RefreshedTokens = blanked }) };
        var authService = CreateAuthService(response, out var handler, out var tokenStorage);

        await authService.ChangePasswordAsync(Request);

        await tokenStorage.DidNotReceive().SaveTokensAsync(Arg.Any<LoginResponse>());
        Assert.Null(handler.LastRequest!.Headers.Authorization);
    }

    [Fact]
    public async Task A_stored_access_token_is_attached_as_a_bearer_header()
    {
        var stored = new LoginResponse { AccessToken = "stored-access", RefreshToken = "stored-refresh", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) };
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ChangePasswordResponse()) };
        var authService = CreateAuthService(response, out var handler, out _, stored);

        await authService.ChangePasswordAsync(Request);

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("stored-access", handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task A_wrong_current_password_comes_back_as_a_coded_refusal()
    {
        var response = Json(HttpStatusCode.BadRequest, """{"code":"current_password_incorrect","message":"The current password is incorrect."}""");
        var authService = CreateAuthService(response, out _, out _);

        var outcome = await authService.ChangePasswordAsync(Request);

        Assert.False(outcome.Succeeded);
        Assert.Equal(ChangePasswordErrorCodes.CurrentPasswordIncorrect, outcome.Code);
        Assert.Equal("The current password is incorrect.", outcome.Message);
    }

    [Fact]
    public async Task A_rejected_new_password_comes_back_with_the_reasons_per_field()
    {
        var response = Json(HttpStatusCode.BadRequest, """{"code":"validation_failed","message":"x","errors":{"newPassword":["Too short.","Needs a digit."]}}""");
        var authService = CreateAuthService(response, out _, out _);

        var outcome = await authService.ChangePasswordAsync(Request);

        Assert.False(outcome.Succeeded);
        Assert.Equal(["Too short.", "Needs a digit."], outcome.FieldErrors["newPassword"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Any_other_failure_throws_with_the_status_code(HttpStatusCode statusCode)
    {
        var authService = CreateAuthService(new HttpResponseMessage(statusCode), out _, out _);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => authService.ChangePasswordAsync(Request));

        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Fact]
    public async Task A_bad_request_without_a_readable_body_throws()
    {
        var authService = CreateAuthService(new HttpResponseMessage(HttpStatusCode.BadRequest), out _, out _);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => authService.ChangePasswordAsync(Request));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }
}
