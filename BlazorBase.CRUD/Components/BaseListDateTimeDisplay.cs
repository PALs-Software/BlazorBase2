using System.Globalization;
using System.Reflection;
using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Core;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Decides how a list shows a date and time: in the viewer's local time when the stored value is a UTC instant.
/// </summary>
/// <remarks>
/// A value is a UTC instant when its <see cref="DateTimeKind"/> says so, or when it carries no kind and its property
/// is an audit field of <see cref="AuditModel"/> or is marked <see cref="UtcDateTimeAttribute"/>: data read from a
/// database or deserialized from JSON without an offset arrives as <see cref="DateTimeKind.Unspecified"/>, so the
/// kind alone cannot tell a UTC stamp from a date-only value. Everything else is shown exactly as stored.
/// <see cref="DateTimeOffset"/> always denotes an instant. "Local" is the time zone of the runtime, which in the
/// browser-hosted client is the viewer's own.
/// </remarks>
internal static class BaseListDateTimeDisplay
{
    private const string DateOnlyFormat = "d";
    private const string DateAndTimeFormat = "g";

    internal static bool IsDateTimeType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        return type == typeof(DateTime) || type == typeof(DateTimeOffset);
    }

    /// <summary>
    /// Returns the value as it is to be displayed: converted to local time when it is a UTC instant, otherwise unchanged.
    /// </summary>
    internal static object? ToDisplayValue(PropertyInfo property, object? value)
    {
        return value switch
        {
            DateTimeOffset offset => offset.ToLocalTime(),
            DateTime dateTime => ToLocal(property, dateTime),
            _ => value
        };
    }

    /// <summary>
    /// Formats a value for a list cell: the explicit format when one is given, otherwise the short date and time, or
    /// only the date when the value has no time of day.
    /// </summary>
    internal static string Format(object value, string? format, CultureInfo culture)
    {
        if (value is not IFormattable formattable)
            return value.ToString() ?? string.Empty;

        if (format is not null)
            return formattable.ToString(format, culture);

        var hasTime = value switch
        {
            DateTime dateTime => dateTime.TimeOfDay != TimeSpan.Zero,
            DateTimeOffset offset => offset.TimeOfDay != TimeSpan.Zero,
            _ => true
        };

        return formattable.ToString(hasTime ? DateAndTimeFormat : DateOnlyFormat, culture);
    }

    private static DateTime ToLocal(PropertyInfo property, DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value.ToLocalTime(),
            DateTimeKind.Local => value,
            _ => IsUtcStamp(property) ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime() : value
        };
    }

    private static bool IsUtcStamp(PropertyInfo property)
    {
        if (property.GetCustomAttribute<UtcDateTimeAttribute>(inherit: true) is not null)
            return true;

        return property.Name is nameof(AuditModel.CreatedOn) or nameof(AuditModel.ModifiedOn)
            && property.ReflectedType is { } owner
            && typeof(AuditModel).IsAssignableFrom(owner);
    }
}
