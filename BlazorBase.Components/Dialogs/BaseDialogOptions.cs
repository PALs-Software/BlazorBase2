using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Dialogs;

/// <summary>
/// What a BlazorBase dialog needs to know before it opens: title, size and the parameters of its content.
/// </summary>
public sealed class BaseDialogOptions
{
    /// <summary>The width of a dialog without an explicit width: 500 px, never wider than the window.</summary>
    public const string DefaultWidth = "min(500px, calc(100vw - 2rem))";

    /// <summary>The class every BlazorBase dialog carries on the Fluent dialog element.</summary>
    public const string DialogClass = "base-dialog";

    /// <summary>The title in the header. Null shows no title.</summary>
    public string? Title { get; set; }

    /// <summary>Any CSS width; used when <see cref="Size"/> is not set.</summary>
    public string? Width { get; set; } = DefaultWidth;

    /// <summary>One of Fluent's fixed dialog sizes; replaces <see cref="Width"/> when set.</summary>
    public DialogSize? Size { get; set; }

    /// <summary>Blocks the page behind the dialog. On by default.</summary>
    public bool Modal { get; set; } = true;

    /// <summary>Keeps Escape from closing the dialog.</summary>
    public bool PreventDismissOnEscape { get; set; }

    /// <summary>Shows the close button (an X) at the end of the header.</summary>
    public bool ShowCloseButton { get; set; }

    /// <summary>The accessible name and tooltip of the close button; pass the localized "Close".</summary>
    public string? CloseButtonLabel { get; set; }

    /// <summary>An additional class for the dialog element; <see cref="DialogClass"/> is always set.</summary>
    public string? Class { get; set; }

    /// <summary>
    /// The parameters of the content component by property name, for example <c>Parameters["Content"] = content</c>.
    /// </summary>
    public IDictionary<string, object?> Parameters { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Called when the dialog is opening, open, closing and closed.</summary>
    public Action<DialogEventArgs>? OnStateChange { get; set; }

    internal DialogOptions ToDialogOptions()
    {
        var options = new DialogOptions
        {
            Class = string.IsNullOrEmpty(Class) ? DialogClass : $"{DialogClass} {Class}",
            Modal = Modal,
            PreventDismissOnEscape = PreventDismissOnEscape,
            Size = Size,
            Width = Size is null ? Width : null,
            Parameters = Parameters,
            OnStateChange = OnStateChange,
        };

        options.Header.Title = Title;
        options.Header.CloseAction.Visible = ShowCloseButton;
        options.Header.CloseAction.Title = CloseButtonLabel;
        options.Header.CloseAction.Tooltip = CloseButtonLabel;
        return options;
    }
}
