using BlazorBase.User.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Wasm;

public static class BlazorBaseUserWasmServiceCollectionExtensions
{
    /// <summary>
    /// Registers the WASM-specific user/auth platform services:
    /// <see cref="BrowserTokenStorage"/> as <see cref="ITokenStorage"/>,
    /// <see cref="WasmAppConfigService"/> as <see cref="IAppConfigService"/> and
    /// <see cref="WasmFormFactor"/> as <see cref="IFormFactor"/>.
    /// The base address is fixed to the host's base address (same-origin API).
    /// </summary>
    public static IServiceCollection AddBlazorBaseUserWasm(this IServiceCollection services, string hostBaseAddress)
    {
        services.AddScoped<ITokenStorage, BrowserTokenStorage>();
        services.AddScoped<IAppConfigService>(_ => new WasmAppConfigService(hostBaseAddress));
        services.AddScoped<IFormFactor, WasmFormFactor>();
        return services;
    }
}
