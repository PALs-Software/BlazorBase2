using System.Linq.Expressions;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Configuration for a list part (1-to-many child collection) embedded in a BaseCard.
/// Supports custom rendering via component type, child card config, or child list config.
/// </summary>
public class BaseListPartConfiguration<TParentModel> where TParentModel : class
{
    public required Expression<Func<TParentModel, object?>> Property { get; set; }

    public string PropertyName { get; internal set; } = string.Empty;

    public bool AllowAdd { get; set; } = true;

    public bool AllowDelete { get; set; } = true;

    public bool AllowReorder { get; set; }

    public Type? ListPartComponentType { get; set; }

    /// <summary>
    /// Optional custom card component rendered in the per-item edit dialog, analogous to
    /// <c>CardType</c> on <c>BaseList</c>. When set, the item dialog renders this component
    /// (in defer-save mode) instead of building a <c>BaseCard</c> from <see cref="ChildCardConfiguration"/>.
    /// </summary>
    public Type? ChildCardType { get; set; }

    public object? ChildCardConfiguration { get; set; }

    public object? ChildListConfiguration { get; set; }

    public List<CrudActionGroup<TParentModel>> ActionGroups { get; set; } = [];
}
