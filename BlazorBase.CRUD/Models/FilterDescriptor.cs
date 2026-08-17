namespace BlazorBase.CRUD.Models;

/// <summary>
/// Describes a filter condition — either a leaf (single property filter) or a group (AND/OR container of child filters).
/// </summary>
public class FilterDescriptor
{
    public string? PropertyName { get; set; }

    public FilterOperator Operator { get; set; }

    public object? Value { get; set; }

    public FilterLogic Logic { get; set; }

    public List<FilterDescriptor>? Filters { get; set; }

    public bool IsGroup => Filters is { Count: > 0 };
}
