using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Security.Claims;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Security;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Builds the list of filterable fields for a model from its scalar properties, honoring the
/// <see cref="FilterConfiguration{TModel}"/> mode/overrides and the current user's read access.
/// </summary>
/// <remarks>
/// A person filters by what they see in the list, so the technical properties - the primary key and the foreign
/// keys that only reference another row - are not offered unless the configuration names them explicitly. Enum
/// members are offered with their display text, resolved like everywhere else by <see cref="EnumLabelResolver"/>.
/// </remarks>
public static class FilterFieldResolver
{
    private static readonly ConcurrentDictionary<Type, List<ScalarProperty>> ScalarCache = new();

    /// <param name="configuration">The filter configuration of the list.</param>
    /// <param name="user">The current user, or null to skip the read access check.</param>
    /// <param name="localizer">The localizer of the model's property labels.</param>
    /// <param name="services">Used to look up the display text of enum members; without it members are shown by name.</param>
    public static List<FilterFieldMetadata> Resolve<TModel>(
        FilterConfiguration<TModel> configuration,
        ClaimsPrincipal? user,
        IStringLocalizer localizer,
        IServiceProvider? services = null) where TModel : class
    {
        if (!configuration.Enabled)
            return [];

        var modelType = typeof(TModel);
        var scalars = ScalarCache.GetOrAdd(modelType, BuildScalarProperties);
        var result = new List<FilterFieldMetadata>();

        foreach (var scalar in scalars)
        {
            if (user is not null)
            {
                var rights = CrudAccessResolver.EvaluateProperty(modelType, scalar.Name, user);
                if ((rights & CrudRights.Read) != CrudRights.Read)
                    continue;
            }

            var fieldConfig = configuration.Fields.Find(f => string.Equals(f.PropertyName, scalar.Name, StringComparison.Ordinal));

            if (scalar.IsTechnical && fieldConfig is null)
                continue;

            if (configuration.Mode == FilterFieldSelectionMode.Include && fieldConfig is null)
                continue;

            if (configuration.Mode == FilterFieldSelectionMode.Exclude && fieldConfig is not null)
                continue;

            var label = fieldConfig?.Label ?? ResolveLabel(localizer, scalar.Name);
            var operators = fieldConfig?.AllowedOperators is { Count: > 0 } configured
                ? configured
                : FilterOperatorCatalog.GetDefaultOperators(scalar.Kind, scalar.IsNullable);

            result.Add(new FilterFieldMetadata(
                scalar.Name, label, scalar.Kind, scalar.ClrType, scalar.IsNullable, [.. operators], scalar.EnumNames,
                ResolveEnumLabels(scalar, localizer, services)));
        }

        return result;
    }

    private static Dictionary<string, string>? ResolveEnumLabels(ScalarProperty scalar, IStringLocalizer localizer, IServiceProvider? services)
    {
        if (services is null || scalar.EnumNames is null)
            return null;

        return scalar.EnumNames.ToDictionary(
            name => name,
            name => EnumLabelResolver.Resolve(services, localizer, scalar.Name, scalar.ClrType, Enum.Parse(scalar.ClrType, name)));
    }

    private static List<ScalarProperty> BuildScalarProperties(Type modelType)
    {
        var list = new List<ScalarProperty>();
        var properties = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            var underlying = Nullable.GetUnderlyingType(property.PropertyType);
            var clrType = underlying ?? property.PropertyType;

            if (!TryClassify(clrType, out var kind))
                continue;

            var isNullable = underlying is not null || !property.PropertyType.IsValueType;
            var enumNames = kind == FilterFieldKind.Enum ? Enum.GetNames(clrType) : null;

            list.Add(new ScalarProperty(property.Name, kind, clrType, isNullable, enumNames, IsTechnical(property, properties)));
        }

        return list;
    }

    private static bool IsTechnical(PropertyInfo property, PropertyInfo[] siblings)
    {
        if (property.Name == "Id" || property.GetCustomAttribute<KeyAttribute>(inherit: true) is not null)
            return true;

        if (property.GetCustomAttribute<ForeignKeyAttribute>(inherit: true) is not null)
            return true;

        if (siblings.Any(other => other.GetCustomAttribute<ForeignKeyAttribute>(inherit: true)?.Name == property.Name))
            return true;

        if (!property.Name.EndsWith("Id", StringComparison.Ordinal) || property.Name.Length <= 2)
            return false;

        var navigationName = property.Name[..^2];

        return siblings.Any(other => other.Name == navigationName && !other.PropertyType.IsValueType && other.PropertyType != typeof(string));
    }

    private static bool TryClassify(Type clrType, out FilterFieldKind kind)
    {
        if (clrType == typeof(string))
        {
            kind = FilterFieldKind.Text;
            return true;
        }

        if (clrType == typeof(bool))
        {
            kind = FilterFieldKind.Boolean;
            return true;
        }

        if (clrType.IsEnum)
        {
            kind = FilterFieldKind.Enum;
            return true;
        }

        if (clrType == typeof(Guid))
        {
            kind = FilterFieldKind.Guid;
            return true;
        }

        if (clrType == typeof(DateTime) || clrType == typeof(DateTimeOffset) || clrType == typeof(DateOnly))
        {
            kind = FilterFieldKind.Date;
            return true;
        }

        if (IsNumeric(clrType))
        {
            kind = FilterFieldKind.Number;
            return true;
        }

        kind = FilterFieldKind.Text;
        return false;
    }

    private static bool IsNumeric(Type type) =>
        type == typeof(byte) || type == typeof(sbyte)
        || type == typeof(short) || type == typeof(ushort)
        || type == typeof(int) || type == typeof(uint)
        || type == typeof(long) || type == typeof(ulong)
        || type == typeof(decimal) || type == typeof(double) || type == typeof(float);

    private static string ResolveLabel(IStringLocalizer localizer, string propertyName)
    {
        var result = localizer[propertyName];
        return result.ResourceNotFound ? propertyName : result.Value;
    }

    private sealed record ScalarProperty(string Name, FilterFieldKind Kind, Type ClrType, bool IsNullable, string[]? EnumNames, bool IsTechnical);
}
