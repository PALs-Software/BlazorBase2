using System.Globalization;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Turns a CLR value into the text a filter condition stores, the same text the filter drawer's editors produce.
/// </summary>
public static class FilterValueFormatter
{
    public static string? Format(object? value) => value switch
    {
        null => null,
        string text => text,
        bool flag => flag ? "true" : "false",
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Enum member => member.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
