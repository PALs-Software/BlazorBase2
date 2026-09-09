using System.Threading;
using BlazorBase.User.Models;
using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.SecurityHarness.Probes.CoreSession;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Services;
using BlazorBase.User.Wasm;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.SecurityHarness.Probes.ClientPipeline;

/// <summary>
/// Composes the real client-side handler pipeline - <see cref="AuthTokenHandler"/> chained ahead of
/// <see cref="SessionExpiryHandler"/>, both wired to a real <see cref="BlazorBaseUserAuthStateProvider"/>
/// and an in-memory <see cref="ITokenStorage"/> - exactly the way a WebAssembly host composes it, and
/// points it at a running harness profile over real HTTP. The only stand-in for what a browser would
/// otherwise provide is the token storage itself; everything else is the framework's own code.
/// </summary>
internal static class ClientPipelineSupport
{
    public static ClientStack BuildClientStack(Uri baseAddress)
    {
        var tokenStorage = new InMemoryTokenStorage();
        var csrfTokenProvider = new CsrfTokenProvider();
        var refreshCounter = new RefreshCallCounter();

        var authHttpClient = new HttpClient(new RefreshCallCountingHandler(refreshCounter) { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false } })
        {
            BaseAddress = baseAddress,
        };
        var authService = new AuthService(authHttpClient);
        var authStateProvider = new BlazorBaseUserAuthStateProvider(tokenStorage, authService, csrfTokenProvider);

        var sessionExpiryHandler = new SessionExpiryHandler(authStateProvider) { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false } };
        var authTokenHandler = new AuthTokenHandler(tokenStorage, authService, authStateProvider) { InnerHandler = sessionExpiryHandler };
        var apiHttpClient = new HttpClient(authTokenHandler) { BaseAddress = baseAddress };

        return new ClientStack(tokenStorage, authStateProvider, authService, apiHttpClient, authHttpClient, refreshCounter);
    }

    /// <summary>Logs in through <paramref name="stack"/>'s own <see cref="IAuthService"/> and adopts the result, exactly as a real login page does.</summary>
    public static async Task<LoginResponse> LoginAndAdoptAsync(ClientStack stack, string email, CancellationToken cancellationToken)
    {
        var outcome = await stack.AuthService.LoginAsync(new LoginRequest { Email = email, Password = CoreSessionSupport.SeedPassword });

        if (outcome.Tokens is null)
            throw new InvalidOperationException("Login did not complete with a token pair.");

        await stack.AuthStateProvider.MarkUserAsAuthenticated(outcome.Tokens);
        return outcome.Tokens;
    }

    /// <summary><c>harness/whoami</c> requires authorization, so the caller's access token must be attached explicitly - this is a plain HTTP call, not routed through any client-side handler.</summary>
    public static async Task<HarnessWhoAmIResponse?> WhoAmIAsync(HttpClient client, Uri baseAddress, string accessToken, CancellationToken cancellationToken)
    {
        var exchange = await CoreSessionSupport.SendAsync(client, HttpMethod.Get, baseAddress, "harness/whoami", bearerToken: accessToken, cancellationToken: cancellationToken);
        CoreSessionSupport.TryDeserialize<HarnessWhoAmIResponse>(exchange.ResponseBody, out var whoAmI);
        return whoAmI;
    }

    /// <summary>Revokes a refresh token's whole family directly, so a probe can force the next refresh attempt to fail without waiting for a replay to be detected over HTTP.</summary>
    public static async Task RevokeRefreshTokenFamilyAsync(ProbeContext context, string refreshToken)
    {
        using var scope = context.Services.CreateScope();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<AuthTokenIssuer<HarnessUser>>();
        var stored = await tokenIssuer.FindAsync(refreshToken);

        if (stored is not null)
            await tokenIssuer.RevokeFamilyAsync(stored.UserId, stored.FamilyId);
    }
}

/// <summary>One composed client - its own token storage, auth state, and the two HttpClients a real host would register (one for IAuthService, one for the rest of the API).</summary>
internal sealed record ClientStack(
    ITokenStorage TokenStorage,
    BlazorBaseUserAuthStateProvider AuthStateProvider,
    IAuthService AuthService,
    HttpClient ApiHttpClient,
    HttpClient AuthHttpClient,
    RefreshCallCounter RefreshCounter) : IDisposable
{
    public void Dispose()
    {
        ApiHttpClient.Dispose();
        AuthHttpClient.Dispose();
    }
}

/// <summary>Counts calls to <c>api/auth/refresh</c>, so a probe can assert "exactly one refresh" instead of inferring it from side effects.</summary>
internal sealed class RefreshCallCounter
{
    private int _count;

    public int Count => _count;

    public void Increment() => Interlocked.Increment(ref _count);
}

internal sealed class RefreshCallCountingHandler(RefreshCallCounter counter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null && request.RequestUri.AbsolutePath.EndsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase))
            counter.Increment();

        return await base.SendAsync(request, cancellationToken);
    }
}
