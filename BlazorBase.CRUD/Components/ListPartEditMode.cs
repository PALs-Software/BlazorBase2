namespace BlazorBase.CRUD.Components;

/// <summary>
/// Determines how items inside a <see cref="BaseListPart{TModel}"/> can be edited.
/// </summary>
public enum ListPartEditMode
{
    /// <summary>Items are read-only — only Add/Remove (if allowed) is available.</summary>
    None,

    /// <summary>Each row renders inline editors for the configured fields.</summary>
    Inline,

    /// <summary>Clicking an item opens a BaseDialog with a deferred-save BaseCard.</summary>
    Dialog
}
