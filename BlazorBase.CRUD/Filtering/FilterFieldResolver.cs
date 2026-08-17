using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Security;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Builds the list of filterable fields for a model from its scalar properties, honoring the
/// <see cref="FilterConfiguration{TModel}"/> mode/overrides and the current user's read access.
/// </summary>
public static class FilterFieldResolver
{
    private static readonly ConcurrentDictionary<Type, List<ScalarProperty>> ScalarCache = new();

    public static List<FilterFieldMetadata> Resolve<TModel>(
        FilterConfiguration<TModel> configuration,
        ClaimsPrincipal? user,
        IStringLocalizer localizer) where TModel : class
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

            if (configuration.Mode == FilterFieldSelectionMode.Include && fieldConfig is null)
                continue;

            if (configuration.Mode == FilterFieldSelectionMode.Exclude && fieldConfig is not null)
                continue;

            var label = fieldConfig?.Label ?? ResolveLabel(localizer, scalar.Name);
            var operators = fieldConfig?.AllowedOperators is { Count: > 0 } configured
                ? configured
                : FilterOperatorCatalog.GetDefaultOperators(scalar.Kind, scalar.IsNullable);

            result.Add(new FilterFieldMetadata(scalar.Name, label, scalar.Kind, scalar.ClrType, scalar.IsNullable, [.. operators], scalar.EnumNames));
        }

        return result;
    }

    private static List<ScalarProperty> BuildScalarProperties(Type modelType)
    {
        var list = new List<ScalarProperty>();

        foreach (var property in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            var underlying = Nullable.GetUnderlyingType(property.PropertyType);
            var clrType = underlying ?? property.PropertyType;

            if (!TryClassify(clrType, out var kind))
                continue;

            var isNullable = underlying is not null || !property.PropertyType.IsValueType;
            var enumNames = kind == FilterFieldKind.Enum ? Enum.GetNames(clrType) : null;

            list.Add(new ScalarProperty(property.Name, kind, clrType, isNullable, enumNames));
        }

        return list;
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

    private sealed record ScalarProperty(string Name, FilterFieldKind Kind, Type ClrType, bool IsNullable, string[]? EnumNames);
}
