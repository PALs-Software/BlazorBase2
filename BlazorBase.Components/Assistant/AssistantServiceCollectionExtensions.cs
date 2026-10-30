using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Assistant;

/// <summary>Registers the assistant panel.</summary>
public static class AssistantServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AssistantClientOptions"/>, <see cref="AssistantPanelState"/>, <see cref="AssistantContextService"/>
    /// and <see cref="SseAssistantChatClient"/> as <see cref="IAssistantChatClient"/>.
    /// </summary>
    /// <remarks>
    /// The default client needs an <see cref="HttpClient"/> in the container, which WebAssembly hosts already have. A
    /// client the host registered before stays, so <c>AddScoped&lt;IAssistantChatClient, ScriptedAssistantChatClient&gt;()</c>
    /// before this call develops the UI without a backend.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adjusts the options.</param>
    public static IServiceCollection AddBlazorBaseAssistant(this IServiceCollection services, Action<AssistantClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOptions<AssistantClientOptions>();

        if (configure is not null)
            builder.Configure(configure);

        return AddServices(services);
    }

    /// <summary>Registers the assistant with the given options instance, which replaces any other options registration.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The options.</param>
    public static IServiceCollection AddBlazorBaseAssistant(this IServiceCollection services, AssistantClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.Replace(ServiceDescriptor.Singleton(Options.Create(options)));
        return AddServices(services);
    }

    private static IServiceCollection AddServices(IServiceCollection services)
    {
        services.TryAddScoped<AssistantPanelState>();
        services.TryAddScoped<AssistantContextService>();
        services.TryAddScoped<IAssistantChatClient>(provider =>
            new SseAssistantChatClient(provider.GetRequiredService<HttpClient>(), provider.GetRequiredService<IOptions<AssistantClientOptions>>()));
        return services;
    }
}
