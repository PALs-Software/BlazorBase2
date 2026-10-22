using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace BlazorBase.Components.Help;

/// <summary>
/// Resolves topics by scanning <see cref="HelpOptions.Assemblies"/> once for routable components
/// (<see cref="RouteAttribute"/>) that carry a <see cref="HelpTopicAttribute"/>.
/// </summary>
/// <remarks>
/// Scanning the assemblies instead of asking the router keeps the host's <c>Router</c> markup untouched: a layout
/// never receives the router's route data, but the URL and the route templates are all that is needed to
/// find the page again.
/// </remarks>
public sealed class HelpTopicResolver(IOptions<HelpOptions> options) : IHelpTopicResolver
{
    private readonly Lazy<HelpRouteMap> Map = new(() => new HelpRouteMap(Scan(options.Value.Assemblies)), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <inheritdoc/>
    public string? ResolveTopic(string? path) => Map.Value.ResolveTopic(path);

    /// <inheritdoc/>
    public string? GetRoute(string topic) => Map.Value.GetRoute(topic);

    /// <summary>Returns every route template of a type carrying a <see cref="HelpTopicAttribute"/> in <paramref name="assemblies"/>, paired with its topic.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Scan(IEnumerable<Assembly> assemblies)
    {
        var routes = new List<KeyValuePair<string, string>>();

        foreach (var assembly in assemblies.Distinct())
            foreach (var type in GetTypes(assembly))
            {
                var topic = type.GetCustomAttribute<HelpTopicAttribute>(inherit: false)?.Topic;

                if (string.IsNullOrWhiteSpace(topic))
                    continue;

                foreach (var route in type.GetCustomAttributes<RouteAttribute>(inherit: false))
                    routes.Add(new KeyValuePair<string, string>(route.Template, topic));
            }

        return routes;
    }

    private static IEnumerable<Type> GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
