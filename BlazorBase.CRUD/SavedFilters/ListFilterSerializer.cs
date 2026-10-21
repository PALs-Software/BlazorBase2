using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorBase.CRUD.Filtering;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Serializes filter trees for storage. Enums are written by name, so a stored filter survives a reordering of the
/// enum members.
/// </summary>
public static class ListFilterSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(FilterGroupModel filter) =>
        JsonSerializer.Serialize(filter, Options);

    /// <summary>
    /// Reads a stored filter tree; unreadable or empty JSON yields an empty filter instead of an exception.
    /// </summary>
    public static FilterGroupModel Deserialize(string? json)
    {
        if (String.IsNullOrWhiteSpace(json))
            return new FilterGroupModel();

        try
        {
            return JsonSerializer.Deserialize<FilterGroupModel>(json, Options) ?? new FilterGroupModel();
        }
        catch (JsonException)
        {
            return new FilterGroupModel();
        }
    }

    public static string SerializeList(IReadOnlyList<SavedListFilterInfo> filters) =>
        JsonSerializer.Serialize(filters, Options);

    public static List<SavedListFilterInfo> DeserializeList(string? json)
    {
        if (String.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<SavedListFilterInfo>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// A stable text of the conditions a filter really applies, used to tell whether two filters are the same.
    /// </summary>
    public static string Canonicalize(FilterGroupModel filter) =>
        JsonSerializer.Serialize(FilterModelConverter.ToDescriptor(filter), Options);
}
