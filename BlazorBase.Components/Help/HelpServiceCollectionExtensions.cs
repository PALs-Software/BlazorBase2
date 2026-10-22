using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.Components.Help;

/// <summary>Registers the contextual help.</summary>
public static class HelpServiceCollectionExtensions
{
    /// <summary>
    /// Registers what <c>BaseLayout</c> needs to show the help panel when its <c>ShowHelp</c> parameter is on: the
    /// <see cref="HelpOptions"/>, the topic resolver and the content source.
    /// </summary>
    /// <remarks>
    /// The default content source reads the Markdown files over HTTP from the same origin (cookies are sent) and therefore needs an <see cref="HttpClient"/>
    /// in the container — WebAssembly hosts already have one, a Blazor Server host registers it with
    /// <c>AddHttpClient()</c>. The host serves the files to signed-in users with <c>MapBlazorBaseHelpContent</c> of the
    /// <c>BlazorBase.Components.Server</c> package; never ship them as anonymous static web assets. Replace <see cref="IHelpContentSource"/> to read the content from elsewhere.
    /// Registration is additive-safe: a service the host registered before stays.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Sets the <see cref="HelpOptions"/>, at least <see cref="HelpOptions.Assemblies"/> for pages carrying a <see cref="HelpTopicAttribute"/>.</param>
    public static IServiceCollection AddBlazorBaseHelp(this IServiceCollection services, Action<HelpOptions>? configure = null)
    {
        var builder = services.AddOptions<HelpOptions>();

        if (configure is not null)
            builder.Configure(configure);

        services.TryAddSingleton<IHelpTopicResolver, HelpTopicResolver>();
        services.TryAddScoped<IHelpContentSource, HelpContentSource>();

        return services;
    }
}
