using System.Net;
using System.Net.Http.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// The endpoint answering with the SPA fallback page (200, text/html) instead of a session document
/// looks identical to a healthy "not signed in" response unless the content type is checked — and it
/// showed up in the shipped template as every user being permanently signed out with no error anywhere.
/// </summary>
public sealed class AuthServiceGetSessionTests
{
    [Fact]
    public async Task GetSessionAsync_JsonSuccess_ReturnsParsedSession()
    {
        var session = new AuthSessionResponse { IsAuthenticated = true, CsrfToken = "token" };
        using var httpClient = new HttpClient(new StubHandler(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(session)
            };
            return response;
        }))
        {
            BaseAddress = new Uri("https://localhost/")
        };
        var authService = new AuthService(httpClient, new NoOpTokenStorage());

        var result = await authService.GetSessionAsync();

        Assert.True(result.IsAuthenticated);
        Assert.Equal("token", result.CsrfToken);
    }

    [Fact]
    public async Task GetSessionAsync_SuccessWithHtmlBody_ThrowsAndNamesTheMissingEndpointMapping()
    {
        using var httpClient = new HttpClient(new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>SPA fallback</body></html>", System.Text.Encoding.UTF8, "text/html")
        }))
        {
            BaseAddress = new Uri("https://localhost/")
        };
        var authService = new AuthService(httpClient, new NoOpTokenStorage());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => authService.GetSessionAsync());

        Assert.Contains("MapBlazorBaseAuthSessionEndpoint", exception.Message);
    }

    [Fact]
    public async Task GetSessionAsync_HttpRequestException_ReturnsUnauthenticatedSession()
    {
        using var httpClient = new HttpClient(new StubHandler(() => throw new HttpRequestException("Network unreachable")))
        {
            BaseAddress = new Uri("https://localhost/")
        };
        var authService = new AuthService(httpClient, new NoOpTokenStorage());

        var result = await authService.GetSessionAsync();

        Assert.False(result.IsAuthenticated);
    }

    [Fact]
    public async Task GetSessionAsync_NonSuccessStatusCode_ReturnsUnauthenticatedSession()
    {
        using var httpClient = new HttpClient(new StubHandler(() => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://localhost/")
        };
        var authService = new AuthService(httpClient, new NoOpTokenStorage());

        var result = await authService.GetSessionAsync();

        Assert.False(result.IsAuthenticated);
    }

    [Fact]
    public async Task GetSessionAsync_WithAnAccessToken_SendsItAsABearerHeader()
    {
        HttpRequestMessage? sentRequest = null;
        using var httpClient = new HttpClient(new RecordingStubHandler(
            request => sentRequest = request,
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AuthSessionResponse { IsAuthenticated = true })
            }))
        {
            BaseAddress = new Uri("https://localhost/")
        };
        var authService = new AuthService(httpClient, new NoOpTokenStorage());

        var result = await authService.GetSessionAsync("an-access-token");

        Assert.True(result.IsAuthenticated);
        Assert.Equal("Bearer", sentRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("an-access-token", sentRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetSessionAsync_WithoutAnAccessToken_SendsNoAuthorizationHeader()
    {
        HttpRequestMessage? sentRequest = null;
        using var httpClient = new HttpClient(new RecordingStubHandler(
            request => sentRequest = request,
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AuthSessionResponse())
            }))
        {
            BaseAddress = new Uri("https://localhost/")
        };
        var authService = new AuthService(httpClient, new NoOpTokenStorage());

        await authService.GetSessionAsync();

        Assert.Null(sentRequest?.Headers.Authorization);
    }

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        #region Injects
        private readonly Func<HttpResponseMessage> Respond = respond;
        #endregion

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Respond());
    }

    private sealed class RecordingStubHandler(Action<HttpRequestMessage> record, Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        #region Injects
        private readonly Action<HttpRequestMessage> Record = record;
        private readonly Func<HttpResponseMessage> Respond = respond;
        #endregion

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Record(request);
            return Task.FromResult(Respond());
        }
    }
}
