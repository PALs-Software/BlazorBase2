using System.Reflection;
using System.Text.Json;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Navigation;
using BlazorBase.CRUD.Security;

namespace BlazorBase.CRUD.DataProviders;

/// <summary>
/// Converts PATCH field values arriving as <see cref="JsonElement"/> (the shape of every value in a
/// <c>Dictionary&lt;string, object?&gt;</c> deserialized from a JSON request body) into the CLR type of
/// the target entity property. Used both to give <see cref="BlazorBase.CRUD.Events.IBaseDataInterceptor{TModel}.OnBeforePatchAsync"/>
/// typed values before it runs and, unchanged, when the patch is finally applied to the entity.
/// </summary>
public static class PatchFieldValueConverter
{
    /// <summary>
    /// Converts every patchable field of <paramref name="changedFields"/> from <see cref="JsonElement"/>
    /// to the CLR type of the matching property on <paramref name="entityType"/>. Fields that do not
    /// resolve to a patchable property (unknown name, read-only, excluded, navigation or audit property)
    /// are passed through unconverted so the existing application behavior for those fields is unchanged.
    /// </summary>
    public static Dictionary<string, object?> ConvertToClrTypes(Type entityType, Dictionary<string, object?> changedFields)
    {
        var converted = new Dictionary<string, object?>(changedFields.Count);

        foreach (var (fieldName, value) in changedFields)
        {
            var property = entityType.GetProperty(fieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            converted[fieldName] = IsPatchableProperty(entityType, property)
                ? ConvertValue(value, property!.PropertyType)
                : value;
        }

        return converted;
    }

    /// <summary>
    /// Converts a single value to <paramref name="propertyType"/>: a JSON null or a CLR null becomes
    /// null, a <see cref="JsonElement"/> is deserialized into the target type, any other value (already
    /// CLR-typed) is returned unchanged.
    /// </summary>
    public static object? ConvertValue(object? value, Type propertyType)
    {
        if (value is null)
            return null;

        if (value is not JsonElement jsonElement)
            return value;

        if (jsonElement.ValueKind == JsonValueKind.Null)
            return null;

        return JsonSerializer.Deserialize(jsonElement.GetRawText(), propertyType);
    }

    private static bool IsPatchableProperty(Type entityType, PropertyInfo? property)
    {
        if (property is null || !property.CanWrite)
            return false;

        if (CrudAccessResolver.IsAlwaysExcluded(entityType, property.Name))
            return false;

        if (NavigationPropertyResolver.GetNavigationProperty(entityType, property.Name) is not null)
            return false;

        if (typeof(AuditModel).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance) is not null)
            return false;

        return true;
    }
}
