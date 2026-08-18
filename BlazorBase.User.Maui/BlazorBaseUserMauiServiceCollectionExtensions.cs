using BlazorBase.User.Services;
using Microsoft.Extensions.DependencyInjection;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Maui;

public static class BlazorBaseUserMauiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the MAUI-specific user/auth platform services:
    /// <see cref="SecureTokenStorage"/> as <see cref="ITokenStorage"/>,
    /// <see cref="MauiAppConfigService"/> as <see cref="IAppConfigService"/> and
    /// <see cref="MauiFormFactor"/> as <see cref="IFormFactor"/>,
    /// all as singletons (consistent with MAUI's app lifetime).
    /// </summary>
    public static IServiceCollection AddBlazorBaseUserMaui(this IServiceCollection services)
    {
        services.AddSingleton<ITokenStorage, SecureTokenStorage>();
        services.AddSingleton<IAppConfigService, MauiAppConfigService>();
        services.AddSingleton<IFormFactor, MauiFormFactor>();
        return services;
    }
}
