using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Display;

/// <summary>
/// Resolves the human-readable display key of an entity — the ordered set of
/// properties that together represent its identity — and renders it as a string.
/// Shared by the BaseCard header and the FK lookup display text so both surfaces
/// agree on how an entity is named.
/// </summary>
public static class DisplayKeyResolver
{
    /// <summary>Default separator placed between multiple display-key values.</summary>
    public const string DefaultSeparator = " · ";

    private const string PrimaryKeyPropertyName = "Id";

    private static readonly string[] ConventionalDisplayPropertyNames =
        ["Name", "Title", "Description", "DisplayName"];

    private static readonly ConcurrentDictionary<Type, IReadOnlyList<string>> AttributeKeyCache = new();

    /// <summary>
    /// Returns the property names carrying <see cref="DisplayKeyAttribute"/> on the
    /// given type, ordered by <see cref="DisplayKeyAttribute.Order"/> then declaration.
    /// Empty when the type declares none.
    /// </summary>
    public static IReadOnlyList<string> GetAttributeDisplayKeyPropertyNames(Type modelType)
    {
        ArgumentNullException.ThrowIfNull(modelType);

        return AttributeKeyCache.GetOrAdd(modelType, static type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (property.Name, Attribute: property.GetCustomAttribute<DisplayKeyAttribute>()))
                .Where(entry => entry.Attribute is not null)
                .OrderBy(entry => entry.Attribute!.Order)
                .Select(entry => entry.Name)
                .ToList());
    }

    /// <summary>Name of the entity's primary key property (the framework convention is <c>Id</c>).</summary>
    public static string GetPrimaryKeyPropertyName(Type modelType) => PrimaryKeyPropertyName;

    /// <summary>
    /// Resolves the display property names for an entity in a lookup context, applying
    /// precedence: explicit override → <see cref="DisplayKeyAttribute"/> set → conventional
    /// name property (<c>Name</c>/<c>Title</c>/<c>Description</c>/<c>DisplayName</c>) → <c>Id</c>.
    /// </summary>
    public static IReadOnlyList<string> ResolveLookupDisplayPropertyNames(Type modelType, string? explicitDisplayProperty)
    {
        ArgumentNullException.ThrowIfNull(modelType);

        if (!string.IsNullOrWhiteSpace(explicitDisplayProperty))
            return [explicitDisplayProperty];

        var attributeKeys = GetAttributeDisplayKeyPropertyNames(modelType);
        if (attributeKeys.Count > 0)
            return attributeKeys;

        return [FindConventionalDisplayPropertyName(modelType)];
    }

    /// <summary>
    /// Returns the first conventional display property present on the type, or <c>Id</c>
    /// when none of the conventional names exist.
    /// </summary>
    public static string FindConventionalDisplayPropertyName(Type modelType)
    {
        ArgumentNullException.ThrowIfNull(modelType);

        foreach (var name in ConventionalDisplayPropertyNames)
        {
            if (modelType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) is not null)
                return name;
        }

        return PrimaryKeyPropertyName;
    }

    /// <summary>
    /// Builds the combined display string from the given property names, reading each
    /// value off <paramref name="model"/>. Null and type-default values (e.g. an empty
    /// GUID or zero key on a new record) are skipped; remaining values are joined with
    /// <paramref name="separator"/>.
    /// </summary>
    public static string BuildDisplayString(object? model, IReadOnlyList<string> propertyNames, string separator)
    {
        if (model is null || propertyNames.Count == 0)
            return string.Empty;

        var modelType = model.GetType();
        var parts = new List<string>(propertyNames.Count);

        foreach (var propertyName in propertyNames)
        {
            var property = modelType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
                continue;

            var value = property.GetValue(model);
            if (IsNullOrDefault(value, property.PropertyType))
                continue;

            var text = value!.ToString();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            parts.Add(text);
        }

        return string.Join(separator, parts);
    }

    private static bool IsNullOrDefault(object? value, Type propertyType)
    {
        if (value is null)
            return true;

        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!underlyingType.IsValueType)
            return false;

        return value.Equals(Activator.CreateInstance(underlyingType));
    }
}
