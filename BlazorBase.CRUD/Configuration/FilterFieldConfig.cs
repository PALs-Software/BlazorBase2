using System.Linq.Expressions;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Per-field filter configuration: selects or excludes a field (depending on the
/// <see cref="FilterConfiguration{TModel}.Mode"/>) and optionally overrides its label and operators.
/// </summary>
public class FilterFieldConfig<TModel>
{
    public required Expression<Func<TModel, object?>> Property { get; set; }

    public string PropertyName { get; internal set; } = string.Empty;

    public string? Label { get; set; }

    public List<FilterOperator>? AllowedOperators { get; set; }

    internal string ResolvePropertyName()
    {
        PropertyName = ExpressionHelper.GetPropertyName(Property);
        return PropertyName;
    }
}
