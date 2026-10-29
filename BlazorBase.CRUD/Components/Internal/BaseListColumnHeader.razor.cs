using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components.Internal;

/// <summary>
/// The content of a column header of <see cref="BaseList{TModel}"/>: the title, as a sort button when the column is sortable.
/// </summary>
public partial class BaseListColumnHeader
{
    /// <summary>The visible column title.</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>The description of the column, shown as the tooltip of the title.</summary>
    [Parameter]
    public string? Tooltip { get; set; }

    /// <summary>The name a screen reader announces for the sort button, including the current sort direction.</summary>
    [Parameter]
    public string? AccessibleName { get; set; }

    /// <summary>The direction the list is sorted by this column, or null when it is not sorted by it.</summary>
    [Parameter]
    public SortDirection? Direction { get; set; }

    /// <summary>Raised when the sort button is activated; without a handler the header is plain text.</summary>
    [Parameter]
    public EventCallback OnSort { get; set; }

    private static Icon SortIcon(SortDirection direction) => direction == SortDirection.Descending
        ? new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.ArrowSortDown()
        : new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.ArrowSortUp();
}
