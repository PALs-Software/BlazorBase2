namespace BlazorBase.CRUD.Models;

/// <summary>
/// Defines the UI locations where a CRUD action can appear.
/// </summary>
[Flags]
public enum CrudActionContext
{
    ListToolbar = 1,
    ContextMenu = 2,
    Card = 4,
    ListPart = 8,
    All = ListToolbar | ContextMenu | Card | ListPart
}
