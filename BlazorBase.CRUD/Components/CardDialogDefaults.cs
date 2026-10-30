using BlazorBase.Components.Dialogs;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The single source of truth for the shape of a dialog that hosts a card. Every framework dialog opener uses
/// it, and a host opening its own card dialog calls <see cref="Create"/> to get exactly the same look.
/// </summary>
public static class CardDialogDefaults
{
    /// <summary>The smallest width a card dialog asks for on a window that allows it.</summary>
    public const string MinimumWidth = "600px";

    /// <summary>The preferred width as a share of the viewport, so a card grows with a wide window.</summary>
    public const string PreferredWidth = "62vw";

    /// <summary>The largest width a card dialog grows to.</summary>
    public const string MaximumWidth = "1100px";

    /// <summary>The distance a card dialog keeps to the viewport edges, so it never exceeds the window.</summary>
    public const string ViewportMargin = "2rem";

    /// <summary>
    /// The CSS width of a card dialog: <c>clamp(600px, 62vw, 1100px)</c>, capped by the viewport minus a margin.
    /// A phone gets the viewport width minus the margin, a tablet roughly 600 px to 700 px, a wide desktop window
    /// up to 1100 px, which lets the card switch to its three-column layout.
    /// </summary>
    public const string Width = $"min(calc(100vw - {ViewportMargin}), clamp({MinimumWidth}, {PreferredWidth}, {MaximumWidth}))";

    /// <summary>
    /// Creates the options of a card dialog: no title bar of its own (the card renders its header and its buttons),
    /// a fluid <see cref="Width"/> and no close button. Escape and a click next to the dialog are handled by the
    /// card, which asks about unsaved changes first.
    /// </summary>
    /// <returns>A new <see cref="BaseDialogOptions"/> instance the caller may adjust.</returns>
    public static BaseDialogOptions Create() => new()
    {
        Title = null,
        Width = Width,
        PreventDismissOnEscape = true,
        ShowCloseButton = false
    };
}

/// <summary>
/// Opens the dialogs that host a card.
/// </summary>
public static class CardDialogServiceExtensions
{
    /// <summary>
    /// Opens a <see cref="BaseDialog{TModel}"/> with the shape of <see cref="CardDialogDefaults"/>. The task completes
    /// when the dialog closes: a saved card returns the model as <see cref="DialogResult.Value"/>.
    /// </summary>
    public static Task<DialogResult> ShowCardDialogAsync<TModel>(this IDialogService dialogService, BaseDialogData<TModel> content)
        where TModel : class, new()
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(content);

        var options = CardDialogDefaults.Create();
        options.Parameters[nameof(BaseDialog<TModel>.Content)] = content;

        return dialogService.ShowBaseDialogAsync<BaseDialog<TModel>>(options);
    }

    /// <summary>
    /// Opens a <see cref="BaseListPartItemDialog{TModel}"/> that edits one item of a list part. A saved card returns
    /// the changed fields as a dictionary in <see cref="DialogResult.Value"/>.
    /// </summary>
    public static Task<DialogResult> ShowListPartItemDialogAsync<TModel>(this IDialogService dialogService, BaseDialogData<TModel> content)
        where TModel : class, new()
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(content);

        var options = CardDialogDefaults.Create();
        options.Parameters[nameof(BaseListPartItemDialog<TModel>.Content)] = content;

        return dialogService.ShowBaseDialogAsync<BaseListPartItemDialog<TModel>>(options);
    }
}
