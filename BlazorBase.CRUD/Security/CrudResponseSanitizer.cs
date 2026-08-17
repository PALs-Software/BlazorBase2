using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Security.Claims;
using BlazorBase.CRUD.Navigation;

namespace BlazorBase.CRUD.Security;

/// <summary>
/// Strips fields a user may not Read from CRUD response payloads before they leave the server.
/// Mirrors <see cref="CrudAccessResolver.EvaluateProperty"/> for scalar properties and, in
/// addition, nulls out (or recurses into) reference and collection navigation properties whose
/// target class or owning navigation is not readable by the user.
/// </summary>
public static class CrudResponseSanitizer
{
    /// <summary>
    /// Strips unreadable fields from every non-null item in <paramref name="items"/>.
    /// </summary>
    public static void Strip<TModel>(IEnumerable<TModel> items, ClaimsPrincipal user)
    {
        foreach (var item in items)
        {
            if (item is null)
                continue;

            Strip(item, user);
        }
    }

    /// <summary>
    /// Strips unreadable fields from <paramref name="item"/>, recursing into readable reference
    /// and collection navigations. Cyclic object graphs are handled safely.
    /// </summary>
    /// <remarks>
    /// Handles a collection here as well, rather than relying on the caller binding to the
    /// <see cref="Strip{TModel}(IEnumerable{TModel}, ClaimsPrincipal)"/> overload. Passing a
    /// <c>List&lt;T&gt;</c> binds to <em>this</em> method with <c>TModel = List&lt;T&gt;</c>,
    /// because that is an identity conversion while the collection overload needs a reference
    /// conversion — and treating the list itself as the entity would leave every element
    /// untouched, which is a leak rather than an error.
    /// </remarks>
    public static void Strip<TModel>(TModel item, ClaimsPrincipal user)
    {
        if (item is null)
            return;

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        if (item is IEnumerable collection and not string)
        {
            var elementType = ResolveElementType(item.GetType());

            foreach (var element in collection)
                StripObject(element, elementType ?? element?.GetType() ?? typeof(object), user, visited);

            return;
        }

        StripObject(item, typeof(TModel), user, visited);
    }

    /// <summary>
    /// The declared element type of a sequence, so rights are evaluated against the model as
    /// declared rather than a runtime subtype such as an EF proxy.
    /// </summary>
    private static Type? ResolveElementType(Type collectionType)
    {
        if (collectionType.IsArray)
            return collectionType.GetElementType();

        if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return collectionType.GetGenericArguments()[0];

        return collectionType.GetInterfaces()
            .Where(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(candidate => candidate.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    private static void StripObject(object? item, Type type, ClaimsPrincipal user, HashSet<object> visited)
    {
        if (item is null)
            return;

        if (!visited.Add(item))
            return;

        StripScalarProperties(item, type, user);
        StripNavigationProperties(item, type, user, visited);
    }

    private static void StripScalarProperties(object item, Type type, ClaimsPrincipal user)
    {
        foreach (var propertyName in CrudAccessResolver.GetPropertyRules(type).Keys)
        {
            if ((CrudAccessResolver.EvaluateProperty(type, propertyName, user) & CrudRights.Read) == CrudRights.Read)
                continue;

            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (property is null || !property.CanWrite)
                continue;

            property.SetValue(item, property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null);
        }
    }

    private static void StripNavigationProperties(object item, Type type, ClaimsPrincipal user, HashSet<object> visited)
    {
        var handledPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var navigation in NavigationPropertyResolver.GetNavigationProperties(type))
        {
            handledPropertyNames.Add(navigation.PropertyName);

            var navigationProperty = type.GetProperty(navigation.PropertyName, BindingFlags.Public | BindingFlags.Instance);

            if (navigationProperty is null || navigationProperty.GetIndexParameters().Length > 0)
                continue;

            var navigationRights = CrudAccessResolver.EvaluateNavigationTarget(type, navigation.PropertyName, navigation.TargetType, user);

            if ((navigationRights & CrudRights.Read) != CrudRights.Read)
            {
                if (navigationProperty.CanWrite)
                    navigationProperty.SetValue(item, null);

                continue;
            }

            var navigationValue = navigationProperty.GetValue(item);

            if (navigationValue is null)
                continue;

            if (navigation.IsCollection)
            {
                foreach (var element in (IEnumerable)navigationValue)
                {
                    if (element is null)
                        continue;

                    StripObject(element, navigation.TargetType, user, visited);
                }

                continue;
            }

            StripObject(navigationValue, navigation.TargetType, user, visited);
        }

        StripComplexReferenceProperties(item, type, user, visited, handledPropertyNames);
    }

    private static void StripComplexReferenceProperties(object item, Type type, ClaimsPrincipal user, HashSet<object> visited, HashSet<string> handledPropertyNames)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (handledPropertyNames.Contains(property.Name))
                continue;

            if (property.GetIndexParameters().Length > 0 || !property.CanRead)
                continue;

            if (property.GetCustomAttribute<NotMappedAttribute>() is not null)
                continue;

            var propertyType = property.PropertyType;

            if (!propertyType.IsClass || propertyType == typeof(string) || typeof(IEnumerable).IsAssignableFrom(propertyType))
                continue;

            var rights = CrudAccessResolver.EvaluateNavigationTarget(type, property.Name, propertyType, user);

            if ((rights & CrudRights.Read) != CrudRights.Read)
            {
                if (property.CanWrite)
                    property.SetValue(item, null);

                continue;
            }

            var value = property.GetValue(item);

            if (value is null)
                continue;

            StripObject(value, propertyType, user, visited);
        }
    }
}
