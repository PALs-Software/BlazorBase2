using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Editable view-model for a single filter condition while the user builds a filter in the panel.
/// Converted to a <see cref="FilterDescriptor"/> on apply.
/// </summary>
public sealed class FilterConditionModel
{
    public string? PropertyName { get; set; }

    public FilterOperator Operator { get; set; }

    public string? Value { get; set; }

    public FilterConditionModel Clone() => new()
    {
        PropertyName = PropertyName,
        Operator = Operator,
        Value = Value
    };
}
