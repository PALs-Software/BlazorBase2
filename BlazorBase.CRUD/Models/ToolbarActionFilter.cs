namespace BlazorBase.CRUD.Models;

/// <summary>
/// Which declared actions a <c>BaseActionToolbar</c> shows, by whether they work on rows.
/// </summary>
public enum ToolbarActionFilter
{
    /// <summary>Every action.</summary>
    All = 0,

    /// <summary>Only the actions that work on rows (scope <see cref="CrudActionScope.Single"/> or <see cref="CrudActionScope.Multiple"/>).</summary>
    ItemBound = 1,

    /// <summary>Only the actions that do not depend on a row (scope <see cref="CrudActionScope.None"/>).</summary>
    ItemIndependent = 2
}
