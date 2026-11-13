using System.Net;
using BlazorBase.User.Services;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// <see cref="AuthService.LogoutAsync"/> completes only for a success status: a logout the server rejected must
/// reach the sign-out service as an exception, because the session is still alive then.
/// </summary>
public sealed class AuthServiceLogoutTests
{
    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        #region Injects
        private readonly HttpStatusCode StatusCode = statusCode;
        #endregion

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(StatusCode));
    }

    private static AuthService CreateAuthService(HttpStatusCode statusCode)
    {
        var httpClient = new HttpClient(new StubHandler(statusCode)) { BaseAddress = new Uri("https://localhost/") };
        return new AuthService(httpClient, Substitute.For<ITokenStorage>());
    }

    [Fact]
    public async Task LogoutAsync_NoContent_Completes()
    {
        var authService = CreateAuthService(HttpStatusCode.NoContent);

        await authService.LogoutAsync("refresh-1");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task LogoutAsync_NonSuccessStatus_Throws(HttpStatusCode statusCode)
    {
        var authService = CreateAuthService(statusCode);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => authService.LogoutAsync("refresh-1"));

        Assert.Equal(statusCode, exception.StatusCode);
    }
}
