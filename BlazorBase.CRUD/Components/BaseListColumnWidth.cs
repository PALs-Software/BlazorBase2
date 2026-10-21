using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Chooses the grid track of a list column that has no explicit width, from the data it holds.
/// </summary>
/// <remarks>
/// Giving every column an equal share cut an e-mail address down to the same prefix in every row while a
/// yes/no column wasted its room. Each kind of value gets the floor it needs to be readable - the grid scrolls
/// sideways rather than clip below it - and a share of the free space in proportion to how much text it tends to
/// hold. The floor also leaves room for the header title on two lines, so a long title is not cut.
/// <c>--base-list-column-min-width</c> still overrides the floor of the plain text columns.
/// </remarks>
internal static class BaseListColumnWidth
{
    private const string TextFloor = "var(--base-list-column-min-width, 10rem)";
    private const string HeaderChrome = "3.5rem";
    private const int MaxHeaderCharacters = 20;
    private const int LongTextLength = 200;

    internal static string Resolve(PropertyInfo property, string title)
    {
        var (floor, share) = ResolveTrack(property);
        var headerFloor = $"calc({HeaderCharacters(title)}ch + {HeaderChrome})";

        return $"minmax(max({floor}, {headerFloor}), {share})";
    }

    private static int HeaderCharacters(string title)
    {
        var longestWord = title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => word.Length).DefaultIfEmpty(0).Max();
        var halfTitle = (int)Math.Ceiling(title.Length / 2.0) + 1;

        return Math.Min(MaxHeaderCharacters, Math.Max(longestWord, halfTitle));
    }

    private static (string Floor, string Share) ResolveTrack(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(bool))
            return ("6rem", "0.5fr");

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return ("11rem", "1fr");

        if (type == typeof(DateOnly))
            return ("8rem", "1fr");

        if (type.IsEnum)
            return ("11rem", "1fr");

        if (IsNumber(type))
            return ("7rem", "0.75fr");

        if (type != typeof(string))
            return (TextFloor, "1fr");

        if (IsEmail(property))
            return ("16rem", "2fr");

        if (IsLongText(property))
            return ("16rem", "2.5fr");

        return IsName(property) ? ("12rem", "1.5fr") : (TextFloor, "1fr");
    }

    private static bool IsNumber(Type type) =>
        Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
            or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

    private static bool IsEmail(PropertyInfo property)
    {
        if (property.GetCustomAttribute<EmailAddressAttribute>(inherit: true) is not null)
            return true;

        if (property.GetCustomAttribute<DataTypeAttribute>(inherit: true)?.DataType == DataType.EmailAddress)
            return true;

        return property.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)
            || property.Name.EndsWith("Mail", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("UserPrincipalName", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLongText(PropertyInfo property)
    {
        if (property.GetCustomAttribute<DataTypeAttribute>(inherit: true)?.DataType == DataType.MultilineText)
            return true;

        return property.GetCustomAttribute<StringLengthAttribute>(inherit: true) is { MaximumLength: > LongTextLength }
            || property.GetCustomAttribute<MaxLengthAttribute>(inherit: true) is { Length: > LongTextLength };
    }

    private static bool IsName(PropertyInfo property) =>
        property.Name.EndsWith("Name", StringComparison.OrdinalIgnoreCase)
        || property.Name.EndsWith("Title", StringComparison.OrdinalIgnoreCase)
        || property.Name.EndsWith("Username", StringComparison.OrdinalIgnoreCase);
}
