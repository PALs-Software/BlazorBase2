using BlazorBase.User.Models;
using BlazorBase.User.Wasm;
using Xunit;

namespace BlazorBase.User.Test.Wasm;

public sealed class InMemoryTokenStorageTests
{
    [Fact]
    public async Task SaveTokensAsync_ThenGetTokensAsync_ReturnsWhatWasStored()
    {
        var storage = new InMemoryTokenStorage();
        var tokens = new LoginResponse { AccessToken = "access", RefreshToken = "refresh", ExpiresAt = DateTime.UtcNow };

        await storage.SaveTokensAsync(tokens);
        var result = await storage.GetTokensAsync();

        Assert.Same(tokens, result);
    }

    [Fact]
    public async Task GetTokensAsync_WithoutASavedToken_ReturnsNull()
    {
        var storage = new InMemoryTokenStorage();

        Assert.Null(await storage.GetTokensAsync());
    }

    [Fact]
    public async Task ClearTokensAsync_RemovesTheStoredToken()
    {
        var storage = new InMemoryTokenStorage();
        await storage.SaveTokensAsync(new LoginResponse { AccessToken = "access", RefreshToken = "refresh" });

        await storage.ClearTokensAsync();

        Assert.Null(await storage.GetTokensAsync());
    }
}
