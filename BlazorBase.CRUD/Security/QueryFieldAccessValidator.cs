using System;
using System.Collections;
using System.Reflection;
using System.Security.Claims;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Navigation;

namespace BlazorBase.CRUD.Security;

/// <summary>
/// Validates that every property path referenced by a <see cref="BaseQuery"/> (filters, sorts
/// and navigation filters) is readable by the requesting user, according to
/// <see cref="CrudAccessResolver"/>. Prevents attacker-controlled filter/sort predicates from
/// being used as an oracle to leak the values of non-readable fields.
/// </summary>
public static class QueryFieldAccessValidator
{
    /// <summary>
    /// Returns the first property path referenced by <paramref name="query"/> that
    /// <paramref name="user"/> may not Read, or <see langword="null"/> when every referenced
    /// field is readable (including the case where the model carries no <c>[CrudAccess]</c>
    /// annotations at all).
    /// </summary>
    public static string? FindUnreadableField(Type modelType, BaseQuery query, ClaimsPrincipal user)
    {
        foreach (var filter in query.Filters)
        {
            var offense = FindOffendingPathInFilter(modelType, filter, user, pathPrefix: null);

            if (offense is not null)
                return offense;
        }

        foreach (var sort in query.Sorts)
        {
            var offense = FindOffendingPath(modelType, sort.PropertyName, user, pathPrefix: null);

            if (offense is not null)
                return offense;
        }

        if (query.NavigationFilters is null)
            return null;

        foreach (var navigationFilter in query.NavigationFilters)
        {
            var offense = FindOffendingPathInNavigationFilter(modelType, navigationFilter, user);

            if (offense is not null)
                return offense;
        }

        return null;
    }

    private static string? FindOffendingPathInNavigationFilter(Type modelType, NavigationFilter navigationFilter, ClaimsPrincipal user)
    {
        var rootOffense = FindOffendingPath(modelType, navigationFilter.NavigationName, user, pathPrefix: null);

        if (rootOffense is not null)
            return rootOffense;

        foreach (var filter in navigationFilter.Filters)
        {
            var offense = FindOffendingPathInFilter(modelType, filter, user, navigationFilter.NavigationName);

            if (offense is not null)
                return offense;
        }

        foreach (var sort in navigationFilter.Sorts)
        {
            var offense = FindOffendingPath(modelType, sort.PropertyName, user, navigationFilter.NavigationName);

            if (offense is not null)
                return offense;
        }

        return null;
    }

    private static string? FindOffendingPathInFilter(Type modelType, FilterDescriptor filter, ClaimsPrincipal user, string? pathPrefix)
    {
        if (filter.IsGroup)
        {
            foreach (var childFilter in filter.Filters!)
            {
                var offense = FindOffendingPathInFilter(modelType, childFilter, user, pathPrefix);

                if (offense is not null)
                    return offense;
            }

            return null;
        }

        return FindOffendingPath(modelType, filter.PropertyName, user, pathPrefix);
    }

    private static string? FindOffendingPath(Type modelType, string? propertyName, ClaimsPrincipal user, string? pathPrefix)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return null;

        var dottedPath = pathPrefix is null ? propertyName : $"{pathPrefix}.{propertyName}";
        return WalkPath(modelType, dottedPath, user);
    }

    private static string? WalkPath(Type ownerType, string dottedPath, ClaimsPrincipal user)
    {
        var segments = dottedPath.Split('.');
        var currentType = ownerType;

        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            var segmentName = segments[segmentIndex];
            var property = currentType.GetProperty(
                segmentName,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property is null)
                return null;

            if ((CrudAccessResolver.EvaluateProperty(currentType, property.Name, user) & CrudRights.Read) != CrudRights.Read)
                return dottedPath;

            var isLastSegment = segmentIndex == segments.Length - 1;

            if (isLastSegment)
                return null;

            var navigation = NavigationPropertyResolver.GetNavigationProperty(currentType, property.Name);

            if (navigation is not null)
            {
                if ((CrudAccessResolver.EvaluateClass(navigation.TargetType, user) & CrudRights.Read) != CrudRights.Read)
                    return dottedPath;

                currentType = navigation.TargetType;
                continue;
            }

            var propertyType = property.PropertyType;

            if (!propertyType.IsClass || propertyType == typeof(string) || typeof(IEnumerable).IsAssignableFrom(propertyType))
                return null;

            if ((CrudAccessResolver.EvaluateClass(propertyType, user) & CrudRights.Read) != CrudRights.Read)
                return dottedPath;

            currentType = propertyType;
        }

        return null;
    }
}
