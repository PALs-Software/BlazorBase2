using System.Reflection;
using System.Security.Claims;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Security;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Decides which of the properties a list was asked to search can take part in the quick search: those that exist on the
/// model, hold text, and that the current user may read.
/// </summary>
/// <remarks>
/// A property the user may not read has to be left out here, because the server refuses a whole query that filters on
/// one (see <see cref="QueryFieldAccessValidator"/>): the search box would otherwise break the list for that user
/// instead of just not searching that field.
/// </remarks>
public static class QuickSearchPropertyResolver
{
    /// <summary>
    /// Returns the requested property paths that are searchable, spelled the way the model declares them.
    /// </summary>
    /// <param name="modelType">The type the list shows.</param>
    /// <param name="requestedPaths">Property names, or dotted paths into a navigation property such as <c>Category.Name</c>.</param>
    /// <param name="user">The current user, or null when no authentication is in play and every property is readable.</param>
    public static List<string> Resolve(Type modelType, IEnumerable<string> requestedPaths, ClaimsPrincipal? user)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        ArgumentNullException.ThrowIfNull(requestedPaths);

        var resolved = new List<string>();

        foreach (var requestedPath in requestedPaths)
        {
            var canonicalPath = ResolveStringPropertyPath(modelType, requestedPath);

            if (canonicalPath is null || resolved.Contains(canonicalPath, StringComparer.Ordinal))
                continue;

            if (user is not null && !IsReadable(modelType, canonicalPath, user))
                continue;

            resolved.Add(canonicalPath);
        }

        return resolved;
    }

    private static string? ResolveStringPropertyPath(Type modelType, string? requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
            return null;

        var segments = requestedPath.Split('.', StringSplitOptions.TrimEntries);
        var canonicalSegments = new List<string>(segments.Length);
        var currentType = modelType;
        PropertyInfo? property = null;

        foreach (var segment in segments)
        {
            property = currentType.GetProperty(segment, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property is null)
                return null;

            canonicalSegments.Add(property.Name);
            currentType = property.PropertyType;
        }

        return property?.PropertyType == typeof(string) ? string.Join('.', canonicalSegments) : null;
    }

    private static bool IsReadable(Type modelType, string propertyPath, ClaimsPrincipal user)
    {
        var probe = new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = propertyPath, Operator = FilterOperator.Contains, Value = string.Empty }]
        };

        return QueryFieldAccessValidator.FindUnreadableField(modelType, probe, user) is null;
    }
}
