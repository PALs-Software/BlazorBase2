using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace BlazorBase.CRUD.Querying;

/// <summary>
/// Coerces a <see cref="Models.FilterDescriptor.Value"/> (which arrives as a CLR value on the
/// server, or as a <see cref="JsonElement"/> after JSON round-tripping over HTTP) into the target
/// property's CLR type. Handles enums, <see cref="Guid"/> and date/time types that
/// <see cref="Convert.ChangeType(object, Type)"/> cannot.
/// </summary>
public static class FilterValueCoercion
{
    public static object? Coerce(object? rawValue, Type targetType)
    {
        if (rawValue is null)
            return null;

        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var value = Normalize(rawValue);

        if (value is null)
            return null;

        if (type.IsInstanceOfType(value))
            return value;

        var text = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(text))
            return null;

        if (type == typeof(string))
            return text;

        if (type.IsEnum)
            return Enum.Parse(type, text, ignoreCase: true);

        if (type == typeof(Guid))
            return Guid.Parse(text);

        if (type == typeof(bool))
            return bool.Parse(text);

        if (type == typeof(DateTime))
            return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        if (type == typeof(DateTimeOffset))
            return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        if (type == typeof(DateOnly))
            return DateOnly.Parse(text, CultureInfo.InvariantCulture);

        if (type == typeof(TimeOnly))
            return TimeOnly.Parse(text, CultureInfo.InvariantCulture);

        if (type == typeof(TimeSpan))
            return TimeSpan.Parse(text, CultureInfo.InvariantCulture);

        return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Coerces a raw collection value (CLR <see cref="IEnumerable"/> or a JSON array
    /// <see cref="JsonElement"/>) into a typed <see cref="List{T}"/> of <paramref name="elementType"/>.
    /// Used to build the typed collection required by the <c>FilterOperator.In</c> expression.
    /// Returns <c>null</c> when <paramref name="rawValue"/> is not a recognisable collection.
    /// </summary>
    public static IList? CoerceCollection(object? rawValue, Type elementType)
    {
        if (rawValue is null)
            return null;

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        if (rawValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in jsonElement.EnumerateArray())
            {
                var coerced = Coerce(item, elementType);
                if (coerced is not null)
                    list.Add(coerced);
            }

            return list;
        }

        if (rawValue is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                var coerced = Coerce(item, elementType);
                if (coerced is not null)
                    list.Add(coerced);
            }

            return list;
        }

        return null;
    }

    private static object? Normalize(object value)
    {
        if (value is not JsonElement json)
            return value;

        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => json.GetRawText()
        };
    }
}
