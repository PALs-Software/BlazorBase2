namespace BlazorBase.CRUD.Models;

/// <summary>
/// Which rows an action works on. Decides where an action is offered by default and when it is enabled.
/// </summary>
public enum CrudActionScope
{
    /// <summary>
    /// The action does not depend on a row. It is always enabled and never offered in the row menu by default.
    /// </summary>
    None = 0,

    /// <summary>
    /// The action works on exactly one row: enabled with exactly one selected row (or the clicked row in a row menu).
    /// </summary>
    Single = 1,

    /// <summary>
    /// The action works on one or more rows: enabled with at least one selected row, and only while every
    /// selected row passes the action's <c>Enabled</c> predicate.
    /// </summary>
    Multiple = 2
}
