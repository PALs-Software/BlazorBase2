using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
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
/// <remarks>
/// Cycle protection tracks both the CLR object reference <em>and</em> the logical entity identity
/// (declared <see cref="KeyAttribute"/> property, falling back to a conventional <c>Id</c>
/// property). The reference check alone is not enough against an EF Core lazy-loading proxy
/// reached through an <c>AsNoTracking()</c> root query: every hop around a navigation cycle can
/// materialize a brand new, non-identity-resolved CLR instance for the same database row, so a
/// purely reference-based visited set never repeats and never stops the walk - this is exactly how
/// a self-referencing or mutually-referencing pair of entities (for example a team lead who is also
/// a member of their own team) drove this walk into a real stack overflow before this fix. Comparing
/// by (declared type, key value) in addition to reference identity stops the walk the moment the
/// same database row is seen again, however many distinct CLR instances represent it.
/// </remarks>
public static class CrudResponseSanitizer
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> EntityKeyProperties = new();

    /// <summary>
    /// The property that identifies an instance of <paramref name="type"/> for cycle-detection
    /// purposes: the <see cref="KeyAttribute"/>-annotated property if one is declared, otherwise a
    /// conventional public <c>Id</c> property. Returns null when neither exists, in which case
    /// cycle detection for that type falls back to reference equality alone.
    /// </summary>
    private static PropertyInfo? ResolveEntityKeyProperty(Type type) =>
        EntityKeyProperties.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() is not null)
            ?? t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance));

    /// <summary>
    /// The (declared type, key value) identity of <paramref name="item"/>, or null when the type has
    /// no resolvable key property. Reading the key property never triggers lazy loading: it is
    /// always a mapped scalar, never a navigation.
    /// </summary>
    private static (Type Type, object Key)? ResolveEntityIdentity(object item, Type type)
    {
        var keyProperty = ResolveEntityKeyProperty(type);
        var keyValue = keyProperty?.GetValue(item);
        return keyValue is null ? null : (type, keyValue);
    }


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

        if (item is IEnumerable collection and not string)
        {
            var elementType = ResolveElementType(item.GetType());

            foreach (var element in collection)
                StripObject(element, elementType ?? element?.GetType() ?? typeof(object), user,
                    new HashSet<object>(ReferenceEqualityComparer.Instance), new HashSet<(Type, object)>());

            return;
        }

        StripObject(item, typeof(TModel), user,
            new HashSet<object>(ReferenceEqualityComparer.Instance), new HashSet<(Type, object)>());
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

    private static void StripObject(object? item, Type type, ClaimsPrincipal user, HashSet<object> visited, HashSet<(Type, object)> visitedIdentities)
    {
        if (item is null)
            return;

        if (!visited.Add(item))
            return;

        var identity = ResolveEntityIdentity(item, type);
        var identityAlreadyVisited = identity is not null && !visitedIdentities.Add(identity.Value);

        StripScalarProperties(item, type, user);

        if (identityAlreadyVisited)
            return;

        StripNavigationProperties(item, type, user, visited, visitedIdentities);
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

    private static void StripNavigationProperties(object item, Type type, ClaimsPrincipal user, HashSet<object> visited, HashSet<(Type, object)> visitedIdentities)
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

                    StripObject(element, navigation.TargetType, user, visited, visitedIdentities);
                }

                continue;
            }

            StripObject(navigationValue, navigation.TargetType, user, visited, visitedIdentities);
        }

        StripComplexReferenceProperties(item, type, user, visited, visitedIdentities, handledPropertyNames);
    }

    private static void StripComplexReferenceProperties(object item, Type type, ClaimsPrincipal user, HashSet<object> visited, HashSet<(Type, object)> visitedIdentities, HashSet<string> handledPropertyNames)
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

            StripObject(value, propertyType, user, visited, visitedIdentities);
        }
    }
}
