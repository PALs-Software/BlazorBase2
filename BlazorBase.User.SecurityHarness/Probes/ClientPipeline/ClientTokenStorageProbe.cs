using BlazorBase.User.SecurityHarness.Hosting;
using BlazorBase.User.Services;
using BlazorBase.User.Wasm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BlazorBase.User.SecurityHarness.Probes.ClientPipeline;

/// <summary>
/// Records which <see cref="ITokenStorage"/> implementation each WebAssembly client-registration
/// method actually selects - a token in <c>localStorage</c> is reachable from any script the page
/// loads, so which one a host picked is worth stating plainly rather than leaving to the option's own
/// doc comment. Purely a DI-registration inspection; no host, no HTTP.
/// </summary>
internal sealed class ClientTokenStorageProbe : IAuthProbe
{
    public string Id => "CLIENT-TOKEN-STORAGE";
    public string Category => "Client pipeline";
    public string Attack => "Inspects which ITokenStorage AddBlazorBaseUserWasm and AddBlazorBaseUserWasmCookieAuth register.";
    public string Expectation => "Records it plainly - a token in localStorage is reachable from any script the page loads.";
    public ProbeSeverity SeverityIfFailed => ProbeSeverity.Info;
    public HarnessProfile Profile => HarnessProfile.Bearer;

    public Task<ProbeResult> RunAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var defaultStorage = ResolveTokenStorage(services => services.AddBlazorBaseUserWasm("https://harness.local"));
        var persistedStorage = ResolveTokenStorage(services => services.AddBlazorBaseUserWasm("https://harness.local", persistTokensInBrowserStorage: true));
        var cookieStorage = ResolveTokenStorage(services => services.AddBlazorBaseUserWasmCookieAuth("https://harness.local"));

        var evidence = new ProbeEvidence(
            "The resolved ITokenStorage implementation type for each registration call.",
            Request: "AddBlazorBaseUserWasm(default), AddBlazorBaseUserWasm(persistTokensInBrowserStorage: true), AddBlazorBaseUserWasmCookieAuth(default)",
            Response: $"default: {defaultStorage.Name}\npersisted: {persistedStorage.Name}\ncookie: {cookieStorage.Name}");

        return Task.FromResult(ProbeResult.Passed(
            $"AddBlazorBaseUserWasm defaults to {defaultStorage.Name} (in-memory, lost on reload). " +
            $"With persistTokensInBrowserStorage: true it registers {persistedStorage.Name} (localStorage - readable by any script on the origin). " +
            $"AddBlazorBaseUserWasmCookieAuth registers {cookieStorage.Name} (holds nothing; the transport owns the session).",
            evidence));
    }

    private static Type ResolveTokenStorage(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime, NeverInvokedJsRuntime>();
        register(services);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ITokenStorage>().GetType();
    }

    /// <summary>
    /// Satisfies <see cref="BrowserTokenStorage"/>'s constructor dependency so the DI container can
    /// build it - this probe only inspects which type was registered, it never calls a method that
    /// would actually invoke JavaScript.
    /// </summary>
    private sealed class NeverInvokedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException("This probe only inspects DI registrations; it never invokes JavaScript.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("This probe only inspects DI registrations; it never invokes JavaScript.");
    }
}
