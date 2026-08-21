using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using BlazorBase.Localization;

namespace BlazorBase.CRUD.Localization;

/// <summary>
/// Builds the two localizer chains used by CRUD components:
/// - Property chain: optional param localizer → model-type localizer → its base-type localizers (up to <see cref="object"/>) → raw key
/// - Framework chain: IStringLocalizer for built-in chrome strings (buttons, dialogs, etc.)
/// </summary>
public static class LocalizerResolver
{
    private static readonly ConcurrentDictionary<Type, Type[]> TypeChainCache = new();

    public static IStringLocalizer ResolveProperty(IServiceProvider services, IStringLocalizer? param, Type modelType)
    {
        var factory = services.GetService<IStringLocalizerFactory>();

        var chain = new List<IStringLocalizer>();
        if (param is not null)
            chain.Add(param);

        if (factory is not null)
            foreach (var type in ModelTypeChain(modelType))
                chain.Add(factory.Create(type));

        return chain.Count switch
        {
            0 => EmptyLocalizer.Instance,
            1 => chain[0],
            _ => new ChainedLocalizer(chain)
        };
    }

    public static IStringLocalizer ResolveFramework(IServiceProvider services)
    {
        var factory = services.GetService<IStringLocalizerFactory>();
        return factory?.Create(typeof(BlazorBaseCrudResources)) ?? EmptyLocalizer.Instance;
    }

    /// <summary>
    /// Looks up an optional key (e.g. a tooltip or placeholder) and returns its value, or
    /// <c>null</c> when the resource is not found — no raw-key fallback.
    /// </summary>
    public static string? ResolveOptional(IStringLocalizer localizer, string key)
    {
        var result = localizer[key];
        return result.ResourceNotFound ? null : result.Value;
    }

    private static Type[] ModelTypeChain(Type modelType) =>
        TypeChainCache.GetOrAdd(modelType, static type =>
        {
            var chain = new List<Type>();
            for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
                chain.Add(current);

            return [.. chain];
        });
}
