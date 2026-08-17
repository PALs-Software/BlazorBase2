using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Resolved, render-ready description of a single filterable field: its name, localized label,
/// kind, the operators offered for it and (for enums) the selectable member names.
/// </summary>
public sealed record FilterFieldMetadata(
    string PropertyName,
    string Label,
    FilterFieldKind Kind,
    Type ClrType,
    bool IsNullable,
    IReadOnlyList<FilterOperator> Operators,
    IReadOnlyList<string>? EnumNames);
