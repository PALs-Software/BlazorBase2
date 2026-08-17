using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace BlazorBase.CRUD.Navigation;

/// <summary>
/// Discovers and caches navigation property metadata for entity types,
/// including FK property resolution via [ForeignKey] attribute and naming conventions.
/// </summary>
public static class NavigationPropertyResolver
{
    private static readonly ConcurrentDictionary<Type, List<NavigationPropertyInfo>> Cache = new();

    public static List<NavigationPropertyInfo> GetNavigationProperties(Type entityType)
    {
        return Cache.GetOrAdd(entityType, BuildNavigationList);
    }

    public static NavigationPropertyInfo? GetNavigationProperty(Type entityType, string propertyName)
    {
        return GetNavigationProperties(entityType)
            .FirstOrDefault(n => n.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }

    public static PropertyInfo? GetForeignKeyProperty(Type entityType, string navigationPropertyName)
    {
        var nav = GetNavigationProperty(entityType, navigationPropertyName);

        if (nav is null || nav.IsCollection)
            return null;

        return nav.ForeignKeyPropertyName is not null
            ? entityType.GetProperty(nav.ForeignKeyPropertyName, BindingFlags.Public | BindingFlags.Instance)
            : null;
    }

    private static List<NavigationPropertyInfo> BuildNavigationList(Type entityType)
    {
        var result = new List<NavigationPropertyInfo>();
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            // An indexer is never a navigation, and reading one without arguments throws.
            if (property.GetIndexParameters().Length > 0)
                continue;

            if (IsCollectionNavigation(property))
            {
                var itemType = GetCollectionItemType(property.PropertyType);

                if (itemType is not null)
                    result.Add(new NavigationPropertyInfo(property.Name, null, itemType, true));

                continue;
            }

            if (IsReferenceNavigation(property))
            {
                var fkName = ResolveForeignKeyName(entityType, property);
                result.Add(new NavigationPropertyInfo(property.Name, fkName, property.PropertyType, false));
            }
        }

        return result;
    }

    private static bool IsCollectionNavigation(PropertyInfo property)
    {
        var type = property.PropertyType;

        if (type == typeof(string))
            return false;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>))
            return true;

        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
    }

    private static bool IsReferenceNavigation(PropertyInfo property)
    {
        var type = property.PropertyType;

        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan)
            || type == typeof(Guid) || type == typeof(byte[])
            || Nullable.GetUnderlyingType(type) is not null)
            return false;

        if (!type.IsClass)
            return false;

        return type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance) is not null;
    }

    private static string? ResolveForeignKeyName(Type entityType, PropertyInfo navigationProperty)
    {
        var fkAttribute = navigationProperty.GetCustomAttribute<ForeignKeyAttribute>();

        if (fkAttribute is not null)
            return fkAttribute.Name;

        var candidateNames = new[]
        {
            $"{navigationProperty.Name}Id",
            $"{navigationProperty.Name}ID"
        };

        foreach (var candidate in candidateNames)
        {
            if (entityType.GetProperty(candidate, BindingFlags.Public | BindingFlags.Instance) is not null)
                return candidate;
        }

        return null;
    }

    private static Type? GetCollectionItemType(Type collectionType)
    {
        if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(ICollection<>))
            return collectionType.GetGenericArguments()[0];

        return collectionType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();
    }
}

public record NavigationPropertyInfo(
    string PropertyName,
    string? ForeignKeyPropertyName,
    Type TargetType,
    bool IsCollection
);
