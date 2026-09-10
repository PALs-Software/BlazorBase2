using System.Net;
using System.Net.Http.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Services;

/// <summary>
/// Proves <see cref="AuthService"/> adopts a refreshed token pair transparently whenever a two-factor
/// management response carries one, so a page that only ever called the pre-existing methods (such as
/// <c>TwoFactorSettings</c>) needs no change of its own to keep its session alive across an operation
/// that rotates the security stamp.
/// </summary>
public sealed class AuthServiceTwoFactorTests
{
    private static readonly LoginResponse RefreshedTokens = new()
    {
        AccessToken = "refreshed-access-token",
        RefreshToken = "refreshed-refresh-token",
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
    };

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        #region Injects
        private readonly Func<HttpResponseMessage> Respond = respond;
        #endregion

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Respond());
    }

    private static AuthService CreateAuthService(HttpResponseMessage response, out ITokenStorage tokenStorage)
    {
        var httpClient = new HttpClient(new StubHandler(() => response)) { BaseAddress = new Uri("https://localhost/") };
        tokenStorage = Substitute.For<ITokenStorage>();
        return new AuthService(httpClient, tokenStorage);
    }

    [Fact]
    public async Task GetTwoFactorStatusAsync_WithRefreshedTokens_PersistsThemToTokenStorage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new TwoFactorStatusResponse { RefreshedTokens = RefreshedTokens }),
        };
        var authService = CreateAuthService(response, out var tokenStorage);

        await authService.GetTwoFactorStatusAsync();

        await tokenStorage.Received(1).SaveTokensAsync(Arg.Is<LoginResponse>(tokens => tokens.AccessToken == RefreshedTokens.AccessToken));
    }

    [Fact]
    public async Task GetTwoFactorStatusAsync_WithoutRefreshedTokens_DoesNotTouchTokenStorage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new TwoFactorStatusResponse()),
        };
        var authService = CreateAuthService(response, out var tokenStorage);

        await authService.GetTwoFactorStatusAsync();

        await tokenStorage.DidNotReceive().SaveTokensAsync(Arg.Any<LoginResponse>());
    }

    [Fact]
    public async Task EnableTwoFactorAsync_WithRefreshedTokens_PersistsThemToTokenStorage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new TwoFactorRecoveryCodesResponse { RecoveryCodes = ["a", "b"], RefreshedTokens = RefreshedTokens }),
        };
        var authService = CreateAuthService(response, out var tokenStorage);

        await authService.EnableTwoFactorAsync(new TwoFactorEnableRequest { Code = "123456" });

        await tokenStorage.Received(1).SaveTokensAsync(Arg.Is<LoginResponse>(tokens => tokens.AccessToken == RefreshedTokens.AccessToken));
    }

    [Fact]
    public async Task DisableTwoFactorAsync_WithRefreshedTokens_PersistsThemToTokenStorage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new TwoFactorDisableResponse { RefreshedTokens = RefreshedTokens }),
        };
        var authService = CreateAuthService(response, out var tokenStorage);

        await authService.DisableTwoFactorAsync(new TwoFactorVerificationRequest { Code = "123456" });

        await tokenStorage.Received(1).SaveTokensAsync(Arg.Is<LoginResponse>(tokens => tokens.AccessToken == RefreshedTokens.AccessToken));
    }
}
