using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Resolved, render-ready description of a single filterable field: its name, localized label,
/// kind, the operators offered for it and (for enums) the selectable member names with the text
/// to show for each.
/// </summary>
/// <remarks>
/// <paramref name="EnumLabels"/> maps a member name to its display text; a member missing from it, or no map at
/// all, is shown by its name.
/// </remarks>
public sealed record FilterFieldMetadata(
    string PropertyName,
    string Label,
    FilterFieldKind Kind,
    Type ClrType,
    bool IsNullable,
    IReadOnlyList<FilterOperator> Operators,
    IReadOnlyList<string>? EnumNames,
    IReadOnlyDictionary<string, string>? EnumLabels = null)
{
    /// <summary>The text to show for an enum member: its localized label, or its name when there is none.</summary>
    public string EnumLabel(string memberName) =>
        EnumLabels is not null && EnumLabels.TryGetValue(memberName, out var label) ? label : memberName;
}
