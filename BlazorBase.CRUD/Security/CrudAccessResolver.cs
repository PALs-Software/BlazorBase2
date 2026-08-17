using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Navigation;

namespace BlazorBase.CRUD.Security;

/// <summary>
/// Parses <see cref="CrudAccessAttribute"/> declarations on entity types and properties,
/// caches the parsed rules per type, and evaluates effective CRUD rights for a given
/// <see cref="ClaimsPrincipal"/>.
/// </summary>
/// <remarks>
/// Hierarchy: Class &gt; Property &gt; View. Each level can only further restrict the
/// rights granted above (intersection). Multiple matching attributes on the same level
/// are combined via union of rights.
/// </remarks>
public static class CrudAccessResolver
{
    public const string WildcardRole = "*";

    private static readonly ConcurrentDictionary<Type, TypeAccessInfo> Cache = new();

    public static IReadOnlyList<CrudAccessRule> GetClassRules(Type entityType) =>
        GetInfo(entityType).ClassRules;

    public static IReadOnlyDictionary<string, IReadOnlyList<CrudAccessRule>> GetPropertyRules(Type entityType) =>
        GetInfo(entityType).PropertyRules;

    public static IReadOnlyList<CrudAccessRule> GetPropertyRules(Type entityType, string propertyName)
    {
        var info = GetInfo(entityType);

        return info.PropertyRules.TryGetValue(propertyName, out var rules)
            ? rules
            : Array.Empty<CrudAccessRule>();
    }

    /// <summary>
    /// True when a property has access rules that grant zero rights to every possible user
    /// (e.g. <c>[CrudAccess("*", "")]</c>). Replaces the old <c>[CrudIgnore]</c> behavior:
    /// such properties are excluded from server-side projection and PATCH application.
    /// </summary>
    public static bool IsAlwaysExcluded(Type entityType, string propertyName)
    {
        var info = GetInfo(entityType);

        if (!info.PropertyRules.TryGetValue(propertyName, out var rules) || rules.Count == 0)
            return false;

        foreach (var rule in rules)
            if (rule.Rights != CrudRights.None)
                return false;

        return true;
    }

    /// <summary>
    /// Effective class-level rights for the principal. Returns <see cref="CrudRights.All"/>
    /// when the entity has no <see cref="CrudAccessAttribute"/> at all.
    /// </summary>
    public static CrudRights EvaluateClass(Type entityType, ClaimsPrincipal user)
    {
        var rules = GetClassRules(entityType);
        return EvaluateRules(rules, user);
    }

    /// <summary>
    /// Effective rights for a single property — intersected with the class-level rights.
    /// Returns <see cref="CrudRights.All"/> when neither class nor property is annotated.
    /// </summary>
    public static CrudRights EvaluateProperty(Type entityType, string propertyName, ClaimsPrincipal user)
    {
        var info = GetInfo(entityType);
        var classRights = EvaluateRules(info.ClassRules, user);

        if (!info.PropertyRules.TryGetValue(propertyName, out var propertyRules))
            return classRights;

        var propertyRights = EvaluateRules(propertyRules, user);
        return classRights & propertyRights;
    }

    /// <summary>
    /// Effective rights at a navigation target — intersected through owner-class, the
    /// navigation property itself, and the target class. Attributes on the target class
    /// act as the absolute upper bound for any navigation-mediated access.
    /// </summary>
    public static CrudRights EvaluateNavigationTarget(
        Type ownerType,
        string navigationPropertyName,
        Type targetType,
        ClaimsPrincipal user)
    {
        var ownerProperty = EvaluateProperty(ownerType, navigationPropertyName, user);
        var target = EvaluateClass(targetType, user);
        return ownerProperty & target;
    }

    /// <summary>
    /// Combines pre-resolved class/property rights (e.g. from attributes) with
    /// rights configured at view level (Card/List builder, markup parameters).
    /// View rights only further restrict.
    /// </summary>
    public static CrudRights ApplyViewRights(CrudRights upper, IReadOnlyList<CrudAccessRule>? viewRules, ClaimsPrincipal user)
    {
        if (viewRules is null || viewRules.Count == 0)
            return upper;

        var viewRights = EvaluateRules(viewRules, user);
        return upper & viewRights;
    }

    /// <summary>
    /// Parses a roles + rights pair into a single <see cref="CrudAccessRule"/>.
    /// Used by fluent builders and markup parameters. Validates the rights characters.
    /// </summary>
    public static CrudAccessRule ParseRule(string roles, string rights, bool allowDelete)
    {
        var parsedRoles = ParseRoles(roles, out var isWildcard);
        var parsedRights = ParseRights(rights, allowDelete, contextDescription: "rule");
        return new CrudAccessRule(parsedRoles, isWildcard, parsedRights);
    }

    /// <summary>
    /// Returns all distinct role names referenced by class- or property-level attributes
    /// on the given type. The wildcard "*" is excluded — use this for role-existence checks.
    /// </summary>
    public static IReadOnlyCollection<string> CollectDeclaredRoles(Type entityType)
    {
        var info = GetInfo(entityType);
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in info.ClassRules)
            CollectRoles(rule, roles);

        foreach (var propertyRules in info.PropertyRules.Values)
            foreach (var rule in propertyRules)
                CollectRoles(rule, roles);

        return roles;
    }

    private static void CollectRoles(CrudAccessRule rule, HashSet<string> sink)
    {
        foreach (var role in rule.Roles)
            if (!string.Equals(role, WildcardRole, StringComparison.Ordinal))
                sink.Add(role);
    }

    private static CrudRights EvaluateRules(IReadOnlyList<CrudAccessRule> rules, ClaimsPrincipal user)
    {
        if (rules.Count == 0)
            return CrudRights.All;

        var effective = CrudRights.None;

        foreach (var rule in rules)
        {
            if (!RuleMatchesUser(rule, user))
                continue;

            effective |= rule.Rights;
        }

        return effective;
    }

    private static bool RuleMatchesUser(CrudAccessRule rule, ClaimsPrincipal user)
    {
        if (rule.IsWildcard)
            return true;

        foreach (var role in rule.Roles)
        {
            if (string.Equals(role, WildcardRole, StringComparison.Ordinal))
                return true;

            if (user.IsInRole(role))
                return true;
        }

        return false;
    }

    private static TypeAccessInfo GetInfo(Type entityType) =>
        Cache.GetOrAdd(entityType, BuildInfo);

    private static TypeAccessInfo BuildInfo(Type entityType)
    {
        var classRules = entityType.GetCustomAttributes<CrudAccessAttribute>(inherit: true)
            .Select(attr => ParseRule(attr.Roles, attr.Rights, allowDelete: true))
            .ToArray();

        var propertyRules = new Dictionary<string, IReadOnlyList<CrudAccessRule>>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attributes = property.GetCustomAttributes<CrudAccessAttribute>(inherit: true).ToArray();

            if (attributes.Length == 0)
                continue;

            var isNavigation = NavigationPropertyResolver.GetNavigationProperty(entityType, property.Name) is not null;

            var rules = attributes
                .Select(attr =>
                {
                    try
                    {
                        return ParseRule(attr.Roles, attr.Rights, allowDelete: isNavigation);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException(
                            $"Invalid [CrudAccess] on {entityType.FullName}.{property.Name}: {ex.Message}",
                            ex);
                    }
                })
                .ToArray();

            propertyRules[property.Name] = rules;
        }

        return new TypeAccessInfo(classRules, propertyRules);
    }

    private static IReadOnlyList<string> ParseRoles(string roles, out bool isWildcard)
    {
        if (roles is null)
            throw new ArgumentException("Roles string must not be null.", nameof(roles));

        var parts = roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            throw new ArgumentException("Roles string must not be empty.", nameof(roles));

        isWildcard = parts.Any(p => string.Equals(p, WildcardRole, StringComparison.Ordinal));
        return parts;
    }

    private static CrudRights ParseRights(string rights, bool allowDelete, string contextDescription)
    {
        if (rights is null)
            throw new ArgumentException("Rights string must not be null.", nameof(rights));

        var result = CrudRights.None;

        foreach (var character in rights)
        {
            var flag = character switch
            {
                'R' or 'r' => CrudRights.Read,
                'I' or 'i' => CrudRights.Insert,
                'M' or 'm' => CrudRights.Modify,
                'D' or 'd' => CrudRights.Delete,
                _ => throw new ArgumentException(
                    $"Invalid rights character '{character}' in '{rights}' on {contextDescription}. " +
                    "Allowed: R, I, M, D.",
                    nameof(rights))
            };

            if (flag == CrudRights.Delete && !allowDelete)
                throw new ArgumentException(
                    $"Rights character 'D' is only allowed at class level or on navigation properties. " +
                    $"Got '{rights}' on {contextDescription}.",
                    nameof(rights));

            result |= flag;
        }

        return result;
    }

    private sealed record TypeAccessInfo(
        IReadOnlyList<CrudAccessRule> ClassRules,
        IReadOnlyDictionary<string, IReadOnlyList<CrudAccessRule>> PropertyRules
    );
}
