using System.Net;
using BlazorBase.User.Services;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// The cookie client holds no refresh token and must still end its session; a bearer client sends
/// its token. A failed logout is reported instead of looking like a closed session.
/// </summary>
public sealed class AuthServiceLogoutTests
{
    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }
        public HttpContent? RequestContent { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.AbsolutePath;
            RequestContent = request.Content;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }

    private static AuthService CreateAuthService(RecordingHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") }, Substitute.For<ITokenStorage>());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task LogoutAsync_WithoutRefreshToken_PostsNoBody(string? refreshToken)
    {
        var handler = new RecordingHandler(HttpStatusCode.NoContent);

        await CreateAuthService(handler).LogoutAsync(refreshToken);

        Assert.Equal("/api/auth/logout", handler.RequestPath);
        Assert.Null(handler.RequestContent);
    }

    [Fact]
    public async Task LogoutAsync_WithRefreshToken_PostsItInTheBody()
    {
        var handler = new RecordingHandler(HttpStatusCode.NoContent);

        await CreateAuthService(handler).LogoutAsync("the-refresh-token");

        Assert.Contains("the-refresh-token", handler.RequestBody);
    }

    [Fact]
    public async Task LogoutAsync_WhenTheServerRefuses_Throws()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateAuthService(handler).LogoutAsync());
    }
}
