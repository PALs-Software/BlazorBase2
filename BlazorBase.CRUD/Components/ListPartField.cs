using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Marker child component for declaring a list part (1-to-many collection) inside a BaseCard.
/// </summary>
public class ListPartField<TModel> : ComponentBase where TModel : class
{
    [Parameter, EditorRequired]
    public Expression<Func<TModel, object?>> Property { get; set; } = default!;

    [Parameter]
    public bool AllowAdd { get; set; } = true;

    [Parameter]
    public bool AllowDelete { get; set; } = true;

    [Parameter]
    public bool AllowReorder { get; set; }

    /// <summary>
    /// Optional custom card component rendered in the per-item edit dialog, analogous to
    /// <c>CardType</c> on <c>BaseList</c>. The component is hosted in defer-save mode.
    /// </summary>
    [Parameter]
    public Type? ChildCardType { get; set; }

    /// <summary>
    /// Optional prebuilt child card configuration (a <c>BaseCardConfiguration&lt;TChild&gt;</c>)
    /// used for the per-item edit card when no <see cref="ChildCardType"/> is set.
    /// </summary>
    [Parameter]
    public object? ChildCardConfiguration { get; set; }

    /// <summary>
    /// Optional custom component that replaces the entire list part rendering, receiving the
    /// child collection as its <c>Items</c> parameter.
    /// </summary>
    [Parameter]
    public Type? ListPartComponentType { get; set; }

    [CascadingParameter]
    private IListPartCollector<TModel>? ListPartCollector { get; set; }

    protected override void OnInitialized()
    {
        var config = new Configuration.BaseListPartConfiguration<TModel>
        {
            Property = Property,
            AllowAdd = AllowAdd,
            AllowDelete = AllowDelete,
            AllowReorder = AllowReorder,
            ChildCardType = ChildCardType,
            ChildCardConfiguration = ChildCardConfiguration,
            ListPartComponentType = ListPartComponentType
        };
        config.PropertyName = Configuration.ExpressionHelper.GetPropertyName(Property);
        ListPartCollector?.AddListPart(config);
    }
}

internal interface IListPartCollector<TModel> where TModel : class
{
    void AddListPart(Configuration.BaseListPartConfiguration<TModel> listPart);
}
