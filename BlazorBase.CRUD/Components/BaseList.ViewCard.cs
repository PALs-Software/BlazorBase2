using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Display;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The card a list opens when neither a <c>CardConfiguration</c> nor a <c>CardType</c> was given: it is built from the
/// columns the user sees in the list, so every list opens something useful, read-only when the user may not edit.
/// </summary>
public partial class BaseList<TModel> where TModel : class, new()
{
    private const int MultiLineTextLength = 35;
    private const int CharactersPerLine = 40;
    private const int MultiLineTextMaxLines = 8;

    /// <summary>
    /// The configuration the card of <paramref name="model"/> uses: the one the list was given, none when a custom
    /// <c>CardType</c> draws the card, otherwise the fallback built from the visible columns.
    /// </summary>
    private BaseCardConfiguration<TModel>? ResolveCardConfiguration(TModel model, bool isNew)
    {
        if (CardConfiguration is not null || CardType is not null)
            return CardConfiguration;

        return BuildFallbackCardConfiguration(model, isNew);
    }

    /// <summary>
    /// A card with one field per visible column, in the order, with the title, the tooltip and the access rules of the
    /// column. A field is editable only when the property has a public setter, is not the key (a new string key aside)
    /// and the column draws no template; the card keeps every field read-only when it opens as a view.
    /// </summary>
    private BaseCardConfiguration<TModel> BuildFallbackCardConfiguration(TModel model, bool isNew)
    {
        var configuration = new BaseCardConfiguration<TModel>();

        foreach (var column in VisibleColumnsForUser)
        {
            if (column.MobileRole == MobileRole.Actions)
                continue;

            if (CreateFallbackField(column, model, isNew) is { } field)
                configuration.Fields.Add(field);
        }

        AddFallbackDisplayKey(configuration);

        return configuration;
    }

    private void AddFallbackDisplayKey(BaseCardConfiguration<TModel> configuration)
    {
        if (DisplayKeyResolver.GetAttributeDisplayKeyPropertyNames(typeof(TModel)).Count > 0)
            return;

        var openColumn = ResolveOpenColumn() ?? VisibleColumnsForUser.FirstOrDefault(column => column.MobileRole != MobileRole.Actions);

        if (openColumn is null || FindRootMember(openColumn.Property.Body) is not { Expression: ParameterExpression } root)
            return;

        configuration.DisplayKeys.Add(new DisplayKeyFieldConfig<TModel> { PropertyName = root.Member.Name });
    }

    private PropertyFieldConfig<TModel>? CreateFallbackField(PropertyColumnConfig<TModel> column, TModel model, bool isNew)
    {
        var body = UnwrapConversion(column.Property.Body);

        if (FindRootMember(body) is not { Member: PropertyInfo property } root)
            return null;

        var isDirect = ReferenceEquals(root, body);

        var field = new PropertyFieldConfig<TModel>
        {
            Property = isDirect
                ? column.Property
                : Expression.Lambda<Func<TModel, object?>>(Expression.Convert(root, typeof(object)), column.Property.Parameters),
            PropertyName = property.Name,
            Label = ResolveColumnTitle(column),
            Tooltip = ResolveColumnTooltip(column),
            AccessRules = column.AccessRules,
            Editable = isDirect && column.Template is null && IsEditableFallbackProperty(property, isNew)
        };

        if (column.Template is { } template)
        {
            field.DisplayTemplate = context => template(context.Model);
        }
        else if (!isDirect || column.Format is not null || IsDateTimeColumn(column))
        {
            field.DisplayTemplate = BuildTextDisplay(column);
            field.HideLabel = true;
        }
        else if (isDirect && property.PropertyType == typeof(string) && CountTextLines(property, model) is var lines and > 0)
        {
            field.FullWidth = true;
            field.Lines = lines;
        }

        return field;
    }

    private static bool IsEditableFallbackProperty(PropertyInfo property, bool isNew)
    {
        if (property.SetMethod is not { IsPublic: true })
            return false;

        var isKey = property.Name == "Id" || property.IsDefined(typeof(KeyAttribute), inherit: true);

        return !isKey || (isNew && property.PropertyType == typeof(string));
    }

    private static int CountTextLines(PropertyInfo property, TModel model)
    {
        if (property.GetValue(model) is not string text)
            return 0;

        var newLines = text.Count(character => character == '\n');

        if (text.Length <= MultiLineTextLength && newLines == 0)
            return 0;

        var wrapped = (text.Length + CharactersPerLine - 1) / CharactersPerLine;

        return Math.Clamp(wrapped + newLines, 2, MultiLineTextMaxLines);
    }

    private RenderFragment<PropertyFieldContext<TModel>> BuildTextDisplay(PropertyColumnConfig<TModel> column)
    {
        return context => builder =>
        {
            builder.OpenComponent<FluentTextField>(0);
            builder.AddComponentParameter(1, nameof(FluentTextField.Label), context.Label);
            builder.AddComponentParameter(2, nameof(FluentTextField.Value), ReadCellTextOrEmpty(column, context.Model));
            builder.AddComponentParameter(3, nameof(FluentTextField.ReadOnly), true);
            builder.CloseComponent();
        };
    }

    private string ReadCellTextOrEmpty(PropertyColumnConfig<TModel> column, TModel item)
    {
        try
        {
            return ResolveCellText(column, item) ?? string.Empty;
        }
        catch (NullReferenceException)
        {
            return string.Empty;
        }
    }

    private static Expression UnwrapConversion(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary ? unary.Operand : expression;

    /// <summary>
    /// The member of the model a column's expression starts at (<c>item.Category</c> for <c>item.Category.Name</c>),
    /// or null when the expression does not read from the model.
    /// </summary>
    private static MemberExpression? FindRootMember(Expression expression)
    {
        var current = UnwrapConversion(expression) as MemberExpression;

        while (current is not null)
        {
            if (current.Expression is ParameterExpression)
                return current;

            current = current.Expression as MemberExpression;
        }

        return null;
    }
}
