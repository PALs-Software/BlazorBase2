using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Fluent builder for the tile a list shows on a narrow screen.
/// </summary>
/// <example>
/// <code>
/// list.MobileCard(card => card
///     .Title(user => user.Email)
///     .Subtitle(user => user.UserName)
///     .Field(user => user.LastSessionCreatedOn)
///     .Badge(user => user.IsInactive));
/// </code>
/// </example>
public class MobileCardBuilder<TModel>
{
    private readonly List<MobileFieldConfig<TModel>> fields = [];
    private int? maxFields;
    private RenderFragment<TModel>? template;
    private RenderFragment<TModel>? actions;

    /// <summary>The headline of the tile.</summary>
    public MobileCardBuilder<TModel> Title(Expression<Func<TModel, object?>> property, string? label = null)
        => Add(property, MobileRole.Title, label);

    /// <summary>A secondary line under the title.</summary>
    public MobileCardBuilder<TModel> Subtitle(Expression<Func<TModel, object?>> property, string? label = null)
        => Add(property, MobileRole.Subtitle, label);

    /// <summary>A label and value pair; call repeatedly, the declaration order is the display order.</summary>
    public MobileCardBuilder<TModel> Field(Expression<Func<TModel, object?>> property, string? label = null)
        => Add(property, MobileRole.Field, label);

    /// <summary>A chip: the enum value, or the column title when a boolean is true.</summary>
    public MobileCardBuilder<TModel> Badge(Expression<Func<TModel, object?>> property, string? label = null)
        => Add(property, MobileRole.Badge, label);

    /// <summary>Keeps a property off the tile although the automatic choice would show it.</summary>
    public MobileCardBuilder<TModel> Hide(Expression<Func<TModel, object?>> property)
        => Add(property, MobileRole.Hidden, null);

    /// <summary>The most label and value pairs on the tile, explicitly declared ones included.</summary>
    public MobileCardBuilder<TModel> MaxFields(int count)
    {
        maxFields = count;
        return this;
    }

    /// <summary>Replaces the tile body (title, subtitle, badges, fields) with custom markup.</summary>
    public MobileCardBuilder<TModel> Template(RenderFragment<TModel> body)
    {
        template = body;
        return this;
    }

    /// <summary>An action row at the bottom of the tile, e.g. the buttons a table row has in its own column.</summary>
    public MobileCardBuilder<TModel> Actions(RenderFragment<TModel> row)
    {
        actions = row;
        return this;
    }

    public MobileCardConfiguration<TModel> Build() => new()
    {
        Fields = [.. fields],
        MaxFields = maxFields,
        Template = template,
        Actions = actions
    };

    private MobileCardBuilder<TModel> Add(Expression<Func<TModel, object?>> property, MobileRole role, string? label)
    {
        var field = new MobileFieldConfig<TModel>
        {
            Property = property,
            Role = role,
            Priority = fields.Count,
            Label = label
        };
        field.PropertyName = ExpressionHelper.GetPropertyName(property);

        fields.Add(field);
        return this;
    }
}
