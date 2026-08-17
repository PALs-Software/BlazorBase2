namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Coarse classification of a filterable property's CLR type, used to pick the available
/// operators and the value editor in the filter panel.
/// </summary>
public enum FilterFieldKind
{
    Text,
    Number,
    Date,
    Boolean,
    Enum,
    Guid
}
