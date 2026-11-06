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
/// Runs the client registrations through a real IHttpClientFactory, which resolves every message
/// handler in a dependency injection scope of its own. The components live in another scope, so any
/// session state that is only scoped is invisible to the handlers: bearer calls went out without a
/// token (401), cookie writes without the antiforgery header (403), and a 401 never reached the UI.
/// </summary>
public sealed class HandlerScopeSessionTests
{
    private const string ApiClientName = "api";
    private const string AccessToken = "access-1";
    private const string AnonymousCsrfToken = "anonymous-token";
    private const string BoundCsrfToken = "bound-token";

    private sealed class FakeApi
    {
        public bool CookieTransport { get; init; }

        public bool SignedIn { get; private set; }

        public HttpStatusCode DataStatusCode { get; set; } = HttpStatusCode.OK;

        public List<HttpRequestMessage> DataRequests { get; } = [];

        public List<HttpRequestMessage> LoginRequests { get; } = [];

        public void EndSession() => SignedIn = false;

        public HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path == "/api/auth/status")
                return Json(new AuthStatusResponse { HasUsers = true, DevelopmentAuthenticationEnabled = true });

            if (path == "/api/auth/session")
                return Json(IsAuthenticated(request) ? AuthenticatedSession() : new AuthSessionResponse { CsrfToken = CookieTransport ? AnonymousCsrfToken : string.Empty });

            if (path == "/api/auth/development-login")
                return DevelopmentLogin(request);

            if (path == "/api/auth/login")
                return Login(request);

            DataRequests.Add(request);
            return new HttpResponseMessage(DataStatusCode);
        }

        private HttpResponseMessage Login(HttpRequestMessage request)
        {
            LoginRequests.Add(request);
            return DevelopmentLogin(request);
        }

        private HttpResponseMessage DevelopmentLogin(HttpRequestMessage request)
        {
            if (CookieTransport && !HasHeader(request, "X-CSRF-TOKEN", AnonymousCsrfToken))
                return new HttpResponseMessage(HttpStatusCode.Forbidden);

            SignedIn = true;
            return Json(new LoginResponse { AccessToken = AccessToken, RefreshToken = "refresh-1", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        }

        private bool IsAuthenticated(HttpRequestMessage request) =>
            CookieTransport ? SignedIn : request.Headers.Authorization?.Parameter == AccessToken;

        private static AuthSessionResponse AuthenticatedSession() => new()
        {
            IsAuthenticated = true,
            CsrfToken = BoundCsrfToken,
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

    private static bool HasHeader(HttpRequestMessage request, string name, string value) =>
        request.Headers.TryGetValues(name, out var values) && values.Contains(value);

    private static ServiceProvider BuildBearerClient(FakeApi api)
    {
        var services = CreateServices(api);
        services.AddBlazorBaseUserClient();
        services.AddBlazorBaseUserWasm("https://localhost/");
        services.AddHttpClient<IAuthService, AuthService>(client => client.BaseAddress = new Uri("https://localhost/"))
            .ConfigurePrimaryHttpMessageHandler(() => new DelegateHandler(api.Respond));
        services.AddHttpClient(ApiClientName, client => client.BaseAddress = new Uri("https://localhost/"))
            .AddHttpMessageHandler<AuthTokenHandler>()
            .AddHttpMessageHandler<SessionExpiryHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new DelegateHandler(api.Respond));
        return BuildProvider(services);
    }

    private static ServiceProvider BuildCookieClient(FakeApi api, bool platformRegistrationFirst = false)
    {
        var services = CreateServices(api);

        if (platformRegistrationFirst)
            services.AddBlazorBaseUserWasmCookieAuth("https://localhost/");

        services.AddBlazorBaseUserClient();

        if (!platformRegistrationFirst)
            services.AddBlazorBaseUserWasmCookieAuth("https://localhost/");

        services.AddHttpClient<IAuthService, AuthService>(client => client.BaseAddress = new Uri("https://localhost/"))
            .AddHttpMessageHandler<CsrfHeaderHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new DelegateHandler(api.Respond));
        services.AddHttpClient(ApiClientName, client => client.BaseAddress = new Uri("https://localhost/"))
            .AddBlazorBaseCookieSessionHandlers()
            .ConfigurePrimaryHttpMessageHandler(() => new DelegateHandler(api.Respond));
        return BuildProvider(services);
    }

    private static ServiceCollection CreateServices(FakeApi api)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddSingleton(api);
        return services;
    }

    private static ServiceProvider BuildProvider(ServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    private static HttpClient CreateApiClient(IServiceProvider appServices) =>
        appServices.GetRequiredService<IHttpClientFactory>().CreateClient(ApiClientName);

    [Fact]
    public async Task Bearer_a_login_in_the_component_scope_reaches_the_token_handler()
    {
        var api = new FakeApi();
        await using var provider = BuildBearerClient(api);
        await using var appScope = provider.CreateAsyncScope();

        await appScope.ServiceProvider.GetRequiredService<BlazorBaseUserAuthStateProvider>().MarkUserAsAuthenticated(
            new LoginResponse { AccessToken = AccessToken, RefreshToken = "refresh-1", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });

        using var response = await CreateApiClient(appScope.ServiceProvider).GetAsync("api/data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AccessToken, Assert.Single(api.DataRequests).Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Bearer_the_development_session_reaches_the_token_handler()
    {
        var api = new FakeApi();
        await using var provider = BuildBearerClient(api);
        await using var appScope = provider.CreateAsyncScope();

        await appScope.ServiceProvider.UseBlazorBaseDevelopmentSessionAsync();
        using var response = await CreateApiClient(appScope.ServiceProvider).GetAsync("api/data");

        Assert.Equal(AccessToken, Assert.Single(api.DataRequests).Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Cookie_the_antiforgery_token_read_by_the_components_reaches_the_csrf_handler()
    {
        var api = new FakeApi { CookieTransport = true };
        await using var provider = BuildCookieClient(api);
        await using var appScope = provider.CreateAsyncScope();

        await appScope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();
        using var response = await CreateApiClient(appScope.ServiceProvider).PostAsync("api/data", content: null);

        Assert.True(HasHeader(Assert.Single(api.DataRequests), "X-CSRF-TOKEN", AnonymousCsrfToken));
    }

    [Fact]
    public async Task Cookie_the_shared_state_does_not_depend_on_the_registration_order()
    {
        var api = new FakeApi { CookieTransport = true };
        await using var provider = BuildCookieClient(api, platformRegistrationFirst: true);
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();

        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<CsrfTokenProvider>(),
            secondScope.ServiceProvider.GetRequiredService<CsrfTokenProvider>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<AuthenticationSessionState>(),
            secondScope.ServiceProvider.GetRequiredService<AuthenticationSessionState>());
    }

    /// <summary>
    /// The development login is a POST like any other, and under the cookie transport it is refused
    /// without the antiforgery token that nothing had fetched yet at start-up.
    /// </summary>
    [Fact]
    public async Task Cookie_the_development_session_signs_in_and_ends_with_the_identity_bound_token()
    {
        var api = new FakeApi { CookieTransport = true };
        await using var provider = BuildCookieClient(api);
        await using var appScope = provider.CreateAsyncScope();

        await appScope.ServiceProvider.UseBlazorBaseDevelopmentSessionAsync();
        var state = await appScope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();
        using var response = await CreateApiClient(appScope.ServiceProvider).PostAsync("api/data", content: null);

        Assert.True(api.SignedIn);
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.True(HasHeader(Assert.Single(api.DataRequests), "X-CSRF-TOKEN", BoundCsrfToken));
    }

    [Fact]
    public async Task A_401_seen_by_a_handler_drops_the_components_provider_to_anonymous()
    {
        var api = new FakeApi { CookieTransport = true };
        await using var provider = BuildCookieClient(api);
        await using var appScope = provider.CreateAsyncScope();
        await appScope.ServiceProvider.UseBlazorBaseDevelopmentSessionAsync();

        var componentProvider = appScope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        var signedIn = await componentProvider.GetAuthenticationStateAsync();
        Task<AuthenticationState>? announced = null;
        componentProvider.AuthenticationStateChanged += task => announced = task;

        api.DataStatusCode = HttpStatusCode.Unauthorized;
        using var response = await CreateApiClient(appScope.ServiceProvider).GetAsync("api/data");
        var afterwards = await componentProvider.GetAuthenticationStateAsync();

        Assert.True(signedIn.User.Identity?.IsAuthenticated);
        Assert.NotNull(announced);
        Assert.False((await announced).User.Identity?.IsAuthenticated);
        Assert.False(afterwards.User.Identity?.IsAuthenticated);
    }

    /// <summary>
    /// The login request is a write that the server refuses without an antiforgery token, and once the
    /// anonymous principal is cached nothing else reads the session again. The invalidation has to
    /// leave the anonymous token behind, or the login form cannot succeed before a page reload.
    /// </summary>
    [Fact]
    public async Task Cookie_a_login_after_the_session_ended_carries_the_anonymous_token()
    {
        var api = new FakeApi { CookieTransport = true };
        await using var provider = BuildCookieClient(api);
        await using var appScope = provider.CreateAsyncScope();
        await appScope.ServiceProvider.UseBlazorBaseDevelopmentSessionAsync();
        await appScope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();

        api.EndSession();
        api.DataStatusCode = HttpStatusCode.Unauthorized;
        using var expired = await CreateApiClient(appScope.ServiceProvider).GetAsync("api/data");
        await appScope.ServiceProvider.GetRequiredService<IAuthService>()
            .LoginAsync(new LoginRequest { Email = "ada@example.test", Password = "secret" });

        Assert.True(HasHeader(Assert.Single(api.LoginRequests), "X-CSRF-TOKEN", AnonymousCsrfToken));
        Assert.True(api.SignedIn);
    }
}
