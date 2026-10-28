using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.Components.HeaderActions;

/// <summary>Registration of header actions.</summary>
public static class HeaderActionsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IHeaderActionService"/>. Calling it more than once is safe; <c>AddBlazorBaseComponents</c> calls it already.</summary>
    public static IServiceCollection AddBlazorBaseHeaderActions(this IServiceCollection services)
    {
        services.TryAddScoped<IHeaderActionService, HeaderActionService>();

        return services;
    }

    /// <summary>Registers a header action, created per scope.</summary>
    public static IServiceCollection AddHeaderAction(this IServiceCollection services, HeaderActionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return services.AddHeaderAction(_ => item);
    }

    /// <summary>Registers a header action built per scope by a factory, e.g. to use a localizer for the title.</summary>
    public static IServiceCollection AddHeaderAction(this IServiceCollection services, Func<IServiceProvider, HeaderActionItem> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        services.AddBlazorBaseHeaderActions();
        services.AddScoped(factory);

        return services;
    }
}
