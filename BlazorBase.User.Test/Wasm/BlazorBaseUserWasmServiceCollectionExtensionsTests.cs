using BlazorBase.User.Services;
using BlazorBase.User.Wasm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace BlazorBase.User.Test.Wasm;

/// <summary>
/// The default must be the storage that does not survive in <c>localStorage</c> — see the client
/// rework closing #5 — with the previous default only reachable through an explicit opt-in.
/// </summary>
public sealed class BlazorBaseUserWasmServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBlazorBaseUserWasm_Default_ResolvesInMemoryTokenStorage()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddBlazorBaseUserWasm("https://localhost/");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<InMemoryTokenStorage>(provider.GetRequiredService<ITokenStorage>());
    }

    [Fact]
    public void AddBlazorBaseUserWasm_ExplicitOptIn_ResolvesBrowserTokenStorage()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddBlazorBaseUserWasm("https://localhost/", persistTokensInBrowserStorage: true);

        using var provider = services.BuildServiceProvider();

        Assert.IsType<BrowserTokenStorage>(provider.GetRequiredService<ITokenStorage>());
    }

    [Fact]
    public void AddBlazorBaseUserWasmCookieAuth_ResolvesNoOpTokenStorage()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddBlazorBaseUserWasmCookieAuth("https://localhost/");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<NoOpTokenStorage>(provider.GetRequiredService<ITokenStorage>());
    }
}
