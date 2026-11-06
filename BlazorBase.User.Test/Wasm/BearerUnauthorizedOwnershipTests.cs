using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using BlazorBase.User.Wasm;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Wasm;

/// <summary>
/// Chains AuthTokenHandler and SessionExpiryHandler on one client of a real IHttpClientFactory and checks
/// that exactly one of them answers a 401: AuthTokenHandler when it holds a refresh token, otherwise
/// SessionExpiryHandler. Whichever handler sits outside must not change the outcome.
/// </summary>
public sealed class BearerUnauthorizedOwnershipTests
{
    private const string ApiClientName = "api";
    private const string InitialAccessToken = "access-1";
    private const string InitialRefreshToken = "refresh-1";
    private const string RenewedAccessToken = "access-2";
    private const string RenewedRefreshToken = "refresh-2";

    private enum RefreshOutcome
    {
        Succeeds,
        IsRejected,
        FailsWithoutVerdict,
    }

    private sealed class FakeApi
    {
        public RefreshOutcome RefreshOutcome { get; set; } = RefreshOutcome.Succeeds;

        public int RefreshRequestCount { get; private set; }

        public List<string?> DataRequestBearerTokens { get; } = [];

        public HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path == "/api/auth/session")
                return Json(request.Headers.Authorization is null ? new AuthSessionResponse() : AuthenticatedSession());

            if (path == "/api/auth/refresh")
                return Refresh();

            DataRequestBearerTokens.Add(request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(request.Headers.Authorization?.Parameter == RenewedAccessToken ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
        }

        private HttpResponseMessage Refresh()
        {
            RefreshRequestCount++;

            return RefreshOutcome switch
            {
                RefreshOutcome.Succeeds => Json(new LoginResponse
                {
                    AccessToken = RenewedAccessToken,
                    RefreshToken = RenewedRefreshToken,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                }),
                RefreshOutcome.IsRejected => new HttpResponseMessage(HttpStatusCode.Unauthorized),
                _ => throw new HttpRequestException("The network is unreachable."),
            };
        }

        private static AuthSessionResponse AuthenticatedSession() => new()
        {
            IsAuthenticated = true,
            Claims = [new AuthSessionClaim(ClaimTypes.Name, "Ada")],
        };

        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        #region Injects
        private readonly Func<HttpRequestMessage, HttpResponseMessage> Respond = respond;
        #endregion

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Respond(request));
    }

    private static ServiceProvider BuildBearerClient(FakeApi api, bool tokenHandlerOutermost)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddBlazorBaseUserClient();
        services.AddBlazorBaseUserWasm("https://localhost/");
        services.AddHttpClient<IAuthService, AuthService>(client => client.BaseAddress = new Uri("https://localhost/"))
            .ConfigurePrimaryHttpMessageHandler(() => new DelegateHandler(api.Respond));

        var apiClientBuilder = services.AddHttpClient(ApiClientName, client => client.BaseAddress = new Uri("https://localhost/"));

        if (tokenHandlerOutermost)
            apiClientBuilder.AddHttpMessageHandler<AuthTokenHandler>().AddHttpMessageHandler<SessionExpiryHandler>();
        else
            apiClientBuilder.AddHttpMessageHandler<SessionExpiryHandler>().AddHttpMessageHandler<AuthTokenHandler>();

        apiClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new DelegateHandler(api.Respond));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task SaveInitialTokensAsync(IServiceProvider provider, string refreshToken) =>
        await provider.GetRequiredService<ITokenStorage>().SaveTokensAsync(new LoginResponse
        {
            AccessToken = InitialAccessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        });

    private static async Task<AuthenticationStateProvider> StartSignedInSessionAsync(IServiceProvider appServices)
    {
        var componentProvider = appServices.GetRequiredService<AuthenticationStateProvider>();
        var signedIn = await componentProvider.GetAuthenticationStateAsync();
        Assert.True(signedIn.User.Identity?.IsAuthenticated);
        return componentProvider;
    }

    private static List<Task<AuthenticationState>> RecordAnnouncements(AuthenticationStateProvider componentProvider)
    {
        var announcements = new List<Task<AuthenticationState>>();
        componentProvider.AuthenticationStateChanged += announcements.Add;
        return announcements;
    }

    private static async Task<List<bool>> AnnouncedAuthenticationAsync(List<Task<AuthenticationState>> announcements)
    {
        var announcedAuthentication = new List<bool>();

        foreach (var announcement in announcements)
            announcedAuthentication.Add((await announcement).User.Identity?.IsAuthenticated == true);

        return announcedAuthentication;
    }

    private static HttpClient CreateApiClient(IServiceProvider appServices) =>
        appServices.GetRequiredService<IHttpClientFactory>().CreateClient(ApiClientName);

    private static async Task AssertRecoveredWithoutAnonymousAnnouncementAsync(bool tokenHandlerOutermost)
    {
        var api = new FakeApi();
        await using var provider = BuildBearerClient(api, tokenHandlerOutermost);
        await using var appScope = provider.CreateAsyncScope();
        await SaveInitialTokensAsync(provider, InitialRefreshToken);
        var componentProvider = await StartSignedInSessionAsync(appScope.ServiceProvider);
        var announcements = RecordAnnouncements(componentProvider);

        using var response = await CreateApiClient(appScope.ServiceProvider).GetAsync("api/data");

        var announcedAuthentication = await AnnouncedAuthenticationAsync(announcements);
        var storedTokens = await provider.GetRequiredService<ITokenStorage>().GetTokensAsync();
        var finalState = await componentProvider.GetAuthenticationStateAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, api.RefreshRequestCount);
        Assert.DoesNotContain(false, announcedAuthentication);
        Assert.Equal(RenewedAccessToken, storedTokens?.AccessToken);
        Assert.Equal(RenewedRefreshToken, storedTokens?.RefreshToken);
        Assert.True(finalState.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public Task Bearer_a_recovered_401_never_announces_anonymous_when_the_token_handler_is_outermost() =>
        AssertRecoveredWithoutAnonymousAnnouncementAsync(tokenHandlerOutermost: true);

    [Fact]
    public Task Bearer_a_recovered_401_never_announces_anonymous_when_the_expiry_handler_is_outermost() =>
        AssertRecoveredWithoutAnonymousAnnouncementAsync(tokenHandlerOutermost: false);

    [Fact]
    public async Task Bearer_a_refresh_rejected_by_the_server_signs_the_user_out()
    {
        var api = new FakeApi { RefreshOutcome = RefreshOutcome.IsRejected };
        await using var provider = BuildBearerClient(api, tokenHandlerOutermost: true);
        await using var appScope = provider.CreateAsyncScope();
        await SaveInitialTokensAsync(provider, InitialRefreshToken);
        var componentProvider = await StartSignedInSessionAsync(appScope.ServiceProvider);
        var announcements = RecordAnnouncements(componentProvider);

        using var response = await CreateApiClient(appScope.ServiceProvider).GetAsync("api/data");

        var announcedAuthentication = await AnnouncedAuthenticationAsync(announcements);
        var finalState = await componentProvider.GetAuthenticationStateAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(false, announcedAuthentication);
        Assert.False(finalState.User.Identity?.IsAuthenticated);
        Assert.Null(await provider.GetRequiredService<ITokenStorage>().GetTokensAsync());
    }

    [Fact]
    public Task Bearer_a_refresh_without_a_verdict_keeps_the_tokens_and_the_session() =>
        AssertKeptSessionAfterRefreshWithoutVerdictAsync(tokenHandlerOutermost: true);

    [Fact]
    public Task Bearer_a_refresh_without_a_verdict_keeps_the_session_when_the_expiry_handler_is_outermost() =>
        AssertKeptSessionAfterRefreshWithoutVerdictAsync(tokenHandlerOutermost: false);

    private static async Task AssertKeptSessionAfterRefreshWithoutVerdictAsync(bool tokenHandlerOutermost)
    {
        var api = new FakeApi { RefreshOutcome = RefreshOutcome.FailsWithoutVerdict };
        await using var provider = BuildBearerClient(api, tokenHandlerOutermost);
        await using var appScope = provider.CreateAsyncScope();
        await SaveInitialTokensAsync(provider, InitialRefreshToken);
        var componentProvider = await StartSignedInSessionAsync(appScope.ServiceProvider);
        var announcements = RecordAnnouncements(componentProvider);

        using var response = await CreateApiClient(appScope.ServiceProvider).GetAsync("api/data");

        var announcedAuthentication = await AnnouncedAuthenticationAsync(announcements);
        var storedTokens = await provider.GetRequiredService<ITokenStorage>().GetTokensAsync();
        var finalState = await componentProvider.GetAuthenticationStateAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, api.RefreshRequestCount);
        Assert.DoesNotContain(false, announcedAuthentication);
        Assert.Equal(InitialAccessToken, storedTokens?.AccessToken);
        Assert.Equal(InitialRefreshToken, storedTokens?.RefreshToken);
        Assert.True(finalState.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Bearer_a_401_without_a_refresh_token_drops_the_session_to_anonymous()
    {
        var api = new FakeApi();
        await using var provider = BuildBearerClient(api, tokenHandlerOutermost: true);
        await using var appScope = provider.CreateAsyncScope();
        await SaveInitialTokensAsync(provider, refreshToken: string.Empty);
        var componentProvider = await StartSignedInSessionAsync(appScope.ServiceProvider);
        var announcements = RecordAnnouncements(componentProvider);

        using var response = await CreateApiClient(appScope.ServiceProvider).GetAsync("api/data");

        var announcedAuthentication = await AnnouncedAuthenticationAsync(announcements);
        var finalState = await componentProvider.GetAuthenticationStateAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, api.RefreshRequestCount);
        Assert.Contains(false, announcedAuthentication);
        Assert.False(finalState.User.Identity?.IsAuthenticated);
        Assert.Null(await provider.GetRequiredService<ITokenStorage>().GetTokensAsync());
    }
}
