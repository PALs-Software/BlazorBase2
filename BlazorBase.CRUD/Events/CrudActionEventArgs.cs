using System.Security.Claims;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Events;

/// <summary>
/// Passed to action callbacks when a CRUD action is executed.
/// </summary>
public class CrudActionEventArgs<TModel> where TModel : class
{
    /// <summary>
    /// The row a <see cref="CrudActionScope.Single"/> action works on (the single selected row, or the clicked row in a
    /// row menu); the clicked row for a <see cref="CrudActionScope.Multiple"/> action in a row menu; otherwise null in a list toolbar.
    /// </summary>
    public TModel? Item { get; init; }

    /// <summary>
    /// The rows the action works on: the selection for a <see cref="CrudActionScope.Multiple"/> action.
    /// </summary>
    public IReadOnlyCollection<TModel> SelectedItems { get; init; } = [];

    public required IServiceProvider ServiceProvider { get; init; }

    public CrudActionContext Context { get; init; }
}

/// <summary>
/// Passed to visibility predicates to determine whether an action should be shown.
/// </summary>
public class CrudActionVisibilityArgs<TModel> where TModel : class
{
    /// <summary>
    /// The row the decision is about: the single selected row or the clicked row; null when no row applies.
    /// </summary>
    public TModel? Item { get; init; }

    /// <summary>
    /// The current selection of the list the decision is about.
    /// </summary>
    public IReadOnlyCollection<TModel> SelectedItems { get; init; } = [];

    public ClaimsPrincipal? User { get; init; }

    public CrudActionContext Context { get; init; }
}
