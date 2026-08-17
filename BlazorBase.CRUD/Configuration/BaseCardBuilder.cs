using System.Linq.Expressions;
using BlazorBase.CRUD.Components.RichTextEditor;
using BlazorBase.CRUD.Components.SanitizedHtml;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Fluent builder for constructing BaseCardConfiguration via code-behind.
/// </summary>
public class BaseCardBuilder<TModel> where TModel : class
{
    private readonly List<PropertyFieldConfig<TModel>> fields = [];
    private readonly List<BaseListPartConfiguration<TModel>> listParts = [];
    private readonly List<CrudActionGroup<TModel>> actionGroups = [];
    private readonly List<DisplayKeyFieldConfig<TModel>> displayKeys = [];
    private int orderCounter;
    private int actionGroupOrderCounter;
    private int displayKeyOrderCounter;
    private string? displayKeySeparator;
    private string? currentGroup;

    public BaseCardBuilder<TModel> Group(string groupName, Action<BaseCardBuilder<TModel>> configure)
    {
        var previousGroup = currentGroup;
        currentGroup = groupName;
        configure(this);
        currentGroup = previousGroup;
        return this;
    }

    public BaseCardBuilder<TModel> Field(
        Expression<Func<TModel, object?>> property,
        Action<FieldBuilder<TModel>>? configure = null)
    {
        var config = new PropertyFieldConfig<TModel>
        {
            Property = property,
            Group = currentGroup,
            Order = orderCounter++
        };
        config.ResolvePropertyName();

        if (configure is not null)
        {
            var builder = new FieldBuilder<TModel>(config);
            configure(builder);
        }

        fields.Add(config);
        return this;
    }

    public BaseCardBuilder<TModel> ListPart<TListModel>(
        Expression<Func<TModel, object?>> property,
        Action<ListPartBuilder<TModel>>? configure = null)
    {
        var config = new BaseListPartConfiguration<TModel>
        {
            Property = property
        };
        config.PropertyName = ExpressionHelper.GetPropertyName(property);

        if (configure is not null)
        {
            var builder = new ListPartBuilder<TModel>(config);
            configure(builder);
        }

        listParts.Add(config);
        return this;
    }

    public BaseCardBuilder<TModel> ActionGroup(
        string name,
        Action<CrudActionGroupBuilder<TModel>> configure)
    {
        var builder = new CrudActionGroupBuilder<TModel>(name);
        builder.Order(actionGroupOrderCounter++);
        configure(builder);
        actionGroups.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Declares a property whose value forms (part of) the card header title, replacing
    /// the primary key. Call multiple times to combine several properties; the optional
    /// <paramref name="order"/> overrides the implicit declaration order.
    /// </summary>
    public BaseCardBuilder<TModel> DisplayKey(
        Expression<Func<TModel, object?>> property,
        int? order = null)
    {
        var config = new DisplayKeyFieldConfig<TModel>
        {
            Property = property,
            Order = order ?? displayKeyOrderCounter++
        };
        config.ResolvePropertyName();

        displayKeys.Add(config);
        return this;
    }

    /// <summary>Sets the separator placed between multiple display-key values in the header title.</summary>
    public BaseCardBuilder<TModel> DisplayKeySeparator(string separator)
    {
        displayKeySeparator = separator;
        return this;
    }

    public BaseCardConfiguration<TModel> Build()
    {
        return new BaseCardConfiguration<TModel>
        {
            Fields = [.. fields],
            ListParts = [.. listParts],
            ActionGroups = [.. actionGroups],
            DisplayKeys = [.. displayKeys],
            DisplayKeySeparator = displayKeySeparator
        };
    }
}

/// <summary>
/// Fluent builder for configuring a single field.
/// </summary>
public class FieldBuilder<TModel>(PropertyFieldConfig<TModel> config)
{
    public FieldBuilder<TModel> Label(string label) { config.Label = label; return this; }
    public FieldBuilder<TModel> Editable(bool editable = true) { config.Editable = editable; return this; }
    public FieldBuilder<TModel> Required(bool required = true) { config.Required = required; return this; }
    public FieldBuilder<TModel> Placeholder(string placeholder) { config.Placeholder = placeholder; return this; }
    public FieldBuilder<TModel> ColSpan(int colSpan) { config.ColSpan = colSpan; return this; }
    public FieldBuilder<TModel> Lines(int lines) { config.Lines = lines; return this; }
    /// <summary>
    /// Adds a view-level access rule. May be called multiple times to grant rights to
    /// additional roles. Rights string accepts R/I/M/D characters; the property's owning
    /// entity must allow these rights at class and property level — view rules can only
    /// further restrict.
    /// </summary>
    public FieldBuilder<TModel> Access(string roles, string rights)
    {
        config.AccessRules.Add(CrudAccessResolver.ParseRule(roles, rights, allowDelete: true));
        return this;
    }
    public FieldBuilder<TModel> LookupThreshold(int threshold) { config.LookupThreshold = threshold; return this; }

    /// <summary>Marks this field's property as a display key contributing to the card header title.</summary>
    public FieldBuilder<TModel> AsDisplayKey(int order = 0) { config.IsDisplayKey = true; config.DisplayKeyOrder = order; return this; }
    public FieldBuilder<TModel> DisplayProperty(string propertyName) { config.DisplayPropertyName = propertyName; return this; }
    public FieldBuilder<TModel> LookupListConfiguration(object listConfiguration) { config.LookupListConfiguration = listConfiguration; return this; }

    /// <summary>
    /// Sets a custom editor template rendered when the field is in edit mode.
    /// The template receives a <see cref="PropertyFieldContext{TModel}"/> carrying the model, current value and a ValueChanged callback.
    /// </summary>
    public FieldBuilder<TModel> EditorTemplate(RenderFragment<PropertyFieldContext<TModel>> template) { config.EditorTemplate = template; return this; }

    /// <summary>
    /// Sets a custom display template rendered when the field is in read/display mode.
    /// The template receives a <see cref="PropertyFieldContext{TModel}"/> carrying the model and current value.
    /// </summary>
    public FieldBuilder<TModel> DisplayTemplate(RenderFragment<PropertyFieldContext<TModel>> template) { config.DisplayTemplate = template; return this; }

    /// <summary>
    /// Convenience method that wires a <see cref="RichTextEditor"/> as the editor template and
    /// a <see cref="SanitizedHtml"/> component as the display template for an HTML body field.
    /// Equivalent to calling <see cref="EditorTemplate"/> and <see cref="DisplayTemplate"/> separately.
    /// </summary>
    public FieldBuilder<TModel> HtmlField()
    {
        config.EditorTemplate = ctx => (RenderTreeBuilder builder) =>
        {
            builder.OpenComponent<RichTextEditor>(0);
            builder.AddAttribute(1, nameof(RichTextEditor.Value), ctx.Value as string);
            builder.AddAttribute(2, nameof(RichTextEditor.ValueChanged),
                EventCallback.Factory.Create<string?>(this, value => ctx.ValueChanged.InvokeAsync(value)));
            builder.CloseComponent();
        };

        config.DisplayTemplate = ctx => (RenderTreeBuilder builder) =>
        {
            builder.OpenComponent<SanitizedHtml>(0);
            builder.AddAttribute(1, nameof(SanitizedHtml.Value), ctx.Value as string);
            builder.CloseComponent();
        };

        return this;
    }
}

/// <summary>
/// Fluent builder for configuring a list part.
/// </summary>
public class ListPartBuilder<TModel>(BaseListPartConfiguration<TModel> config) where TModel : class
{
    public ListPartBuilder<TModel> AllowAdd(bool allow = true) { config.AllowAdd = allow; return this; }
    public ListPartBuilder<TModel> AllowDelete(bool allow = true) { config.AllowDelete = allow; return this; }
    public ListPartBuilder<TModel> AllowReorder(bool allow = true) { config.AllowReorder = allow; return this; }
    public ListPartBuilder<TModel> ComponentType(Type componentType) { config.ListPartComponentType = componentType; return this; }
    public ListPartBuilder<TModel> ComponentType<TComponent>() where TComponent : Microsoft.AspNetCore.Components.ComponentBase { config.ListPartComponentType = typeof(TComponent); return this; }

    /// <summary>Renders a custom card component in the per-item edit dialog (defer-save mode), analogous to <c>CardType</c> on <c>BaseList</c>.</summary>
    public ListPartBuilder<TModel> ChildCardType(Type cardComponentType) { config.ChildCardType = cardComponentType; return this; }

    /// <summary>Renders a custom card component in the per-item edit dialog (defer-save mode), analogous to <c>CardType</c> on <c>BaseList</c>.</summary>
    public ListPartBuilder<TModel> ChildCardType<TComponent>() where TComponent : Microsoft.AspNetCore.Components.ComponentBase { config.ChildCardType = typeof(TComponent); return this; }

    public ListPartBuilder<TModel> ChildCardConfig(object cardConfiguration) { config.ChildCardConfiguration = cardConfiguration; return this; }
    public ListPartBuilder<TModel> ChildListConfig(object listConfiguration) { config.ChildListConfiguration = listConfiguration; return this; }
}
