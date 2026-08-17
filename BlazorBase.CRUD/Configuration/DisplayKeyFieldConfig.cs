using System;
using System.Linq.Expressions;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Declares a single property as part of an entity's display key, with an order
/// determining its position in the combined display string. Produced by the fluent
/// <c>BaseCardBuilder.DisplayKey</c> method or the <c>DisplayKeyField</c> markup component.
/// </summary>
public class DisplayKeyFieldConfig<TModel>
{
    public Expression<Func<TModel, object?>>? Property { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public int Order { get; set; }

    internal string ResolvePropertyName()
    {
        if (!string.IsNullOrEmpty(PropertyName))
            return PropertyName;

        if (Property is null)
            throw new InvalidOperationException("DisplayKeyFieldConfig requires either Property or PropertyName.");

        PropertyName = ExpressionHelper.GetPropertyName(Property);
        return PropertyName;
    }
}
