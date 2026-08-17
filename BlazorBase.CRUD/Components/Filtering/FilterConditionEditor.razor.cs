using System.Globalization;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components.Filtering;

public partial class FilterConditionEditor
{
    [Parameter, EditorRequired]
    public FilterConditionModel Condition { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyList<FilterFieldMetadata> Fields { get; set; } = [];

    [Parameter, EditorRequired]
    public IStringLocalizer Localizer { get; set; } = default!;

    [Parameter]
    public EventCallback OnRemove { get; set; }

    private FilterFieldMetadata? SelectedField =>
        Fields.FirstOrDefault(f => f.PropertyName == Condition.PropertyName);

    private bool ShowValueEditor =>
        SelectedField is not null && !FilterModelConverter.IsValueLess(Condition.Operator);

    private void OnFieldChanged(string? propertyName)
    {
        Condition.PropertyName = string.IsNullOrEmpty(propertyName) ? null : propertyName;

        var field = Fields.FirstOrDefault(f => f.PropertyName == Condition.PropertyName);
        Condition.Operator = field is { Operators.Count: > 0 } ? field.Operators[0] : FilterOperator.Equals;
        Condition.Value = null;
    }

    private string OperatorValue
    {
        get => Condition.Operator.ToString();
        set
        {
            if (!Enum.TryParse<FilterOperator>(value, out var op))
                return;

            Condition.Operator = op;

            if (FilterModelConverter.IsValueLess(op))
                Condition.Value = null;
        }
    }

    private string OperatorLabel(FilterOperator op)
    {
        var result = Localizer[$"FilterOperator_{op}"];
        return result.ResourceNotFound ? op.ToString() : result.Value;
    }

    private string? TextValue
    {
        get => Condition.Value;
        set => Condition.Value = value;
    }

    private DateTime? DateValue
    {
        get => DateTime.TryParse(Condition.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
        set => Condition.Value = value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
