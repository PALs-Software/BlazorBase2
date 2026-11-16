using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorBase.CRUD.Core;

/// <summary>
/// Resolves the single-property primary key of an entity type: a property named <c>Id</c>, otherwise the property
/// carrying <see cref="KeyAttribute"/>, otherwise <c>&lt;TypeName&gt;Id</c> (the EF Core conventions). A composite
/// key resolves to nothing.
/// </summary>
public static class EntityKeyResolver
{
    /// <summary>The key name assumed when a type has no resolvable key property.</summary>
    public const string DefaultKeyName = "Id";

    private static readonly ConcurrentDictionary<Type, PropertyInfo?> Cache = new();

    /// <summary>Returns the key property of <paramref name="type"/>, or null when it has none or a composite key.</summary>
    public static PropertyInfo? Find(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Cache.GetOrAdd(type, Resolve);
    }

    /// <summary>Returns the name of the key property, <see cref="DefaultKeyName"/> when none resolves.</summary>
    public static string GetName(Type type) => Find(type)?.Name ?? DefaultKeyName;

    /// <summary>Returns the key value of <paramref name="entity"/>, or null when the type has no key property.</summary>
    public static object? GetValue(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Find(entity.GetType())?.GetValue(entity);
    }

    private static PropertyInfo? Resolve(Type type)
    {
        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToList();

        var named = properties.FirstOrDefault(property => property.Name == DefaultKeyName);

        if (named is not null)
            return named;

        var attributed = properties.Where(property => property.IsDefined(typeof(KeyAttribute), inherit: true)).ToList();

        if (attributed.Count > 0)
            return attributed.Count == 1 ? attributed[0] : null;

        return properties.FirstOrDefault(property => string.Equals(property.Name, type.Name + "Id", StringComparison.OrdinalIgnoreCase));
    }
}
