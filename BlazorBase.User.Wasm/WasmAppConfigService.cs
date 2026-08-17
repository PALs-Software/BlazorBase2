using BlazorBase.User.Services;

namespace BlazorBase.User.Wasm;

public class WasmAppConfigService(string baseAddress) : IAppConfigService
{
    private readonly string BaseAddress = baseAddress.TrimEnd('/');

    public Task<string?> GetServerUrlAsync() => Task.FromResult<string?>(BaseAddress);
    public Task SaveServerUrlAsync(string url) => Task.CompletedTask;
    public Task<bool> IsConfiguredAsync() => Task.FromResult(true);
}
