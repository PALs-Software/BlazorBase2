using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Services;

/// <summary>
/// Registers the Fluent UI services with the defaults the BlazorBase layout is built for.
/// </summary>
public static class BlazorBaseFluentUiServiceCollectionExtensions
{
    /// <summary>The distance from the top of the window to a toast: the header height plus a gap.</summary>
    public const int ToastVerticalOffset = 64;

    /// <summary>The most toasts shown at once; further ones queue.</summary>
    public const int MaxToastCount = 3;

    /// <summary>
    /// Calls <c>AddFluentUIComponents</c> with toasts at the top end of the window below the header, at most
    /// <see cref="MaxToastCount"/> at a time, and fields without Fluent's default 12px vertical margin (the layouts
    /// set their own gaps). <paramref name="configure"/> runs afterwards and can change anything.
    /// Use it instead of calling <c>AddFluentUIComponents</c> yourself.
    /// </summary>
    public static IServiceCollection AddBlazorBaseFluentUI(this IServiceCollection services, Action<LibraryConfiguration>? configure = null)
    {
        return services.AddFluentUIComponents(configuration =>
        {
            configuration.Toast.MaxToastCount = MaxToastCount;
            configuration.Toast.Position = ToastPosition.TopEnd;
            configuration.Toast.VerticalOffset = ToastVerticalOffset;
            configuration.DefaultStyles.FluentFieldClass = null;
            configure?.Invoke(configuration);
        });
    }
}
