using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Opens BlazorBase dialogs through Fluent's <see cref="IDialogService"/>. Every dialog of the framework and of an
/// application goes through these methods, so they share the dialog class, the default width and the result model.
/// </summary>
/// <remarks>
/// The returned task completes when the dialog is closed, by a button, Escape or a click on the backdrop; its
/// <see cref="DialogResult.Cancelled"/> tells which. Close it from the outside through the
/// <see cref="IDialogInstance"/> in <see cref="DialogEventArgs.Instance"/> of <see cref="BaseDialogOptions.OnStateChange"/>.
/// </remarks>
public static class BaseDialogServiceExtensions
{
    /// <summary>Opens <typeparamref name="TDialog"/> with the given options and waits for it to close.</summary>
    public static Task<DialogResult> ShowBaseDialogAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDialog>(
        this IDialogService dialogService,
        BaseDialogOptions options)
        where TDialog : ComponentBase
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(options);

        return dialogService.ShowDialogAsync<TDialog>(options.ToDialogOptions());
    }

    /// <summary>Opens <typeparamref name="TDialog"/> after <paramref name="configure"/> has filled in the options.</summary>
    public static Task<DialogResult> ShowBaseDialogAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDialog>(
        this IDialogService dialogService,
        Action<BaseDialogOptions> configure)
        where TDialog : ComponentBase
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new BaseDialogOptions();
        configure(options);
        return dialogService.ShowBaseDialogAsync<TDialog>(options);
    }

    /// <summary>
    /// Opens a dialog whose content is a <see cref="BaseDialogInstance{TContent}"/>: the title and optional width go
    /// into the options and <paramref name="content"/> becomes the content's <c>Content</c> parameter.
    /// </summary>
    public static Task<DialogResult> ShowBaseDialogAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDialog, TContent>(
        this IDialogService dialogService,
        string title,
        TContent content,
        string? width = null)
        where TDialog : BaseDialogInstance<TContent>
        where TContent : notnull
    {
        ArgumentNullException.ThrowIfNull(content);

        return dialogService.ShowBaseDialogAsync<TDialog>(options =>
        {
            options.Title = title;
            options.Parameters[nameof(BaseDialogInstance<TContent>.Content)] = content;

            if (width is not null)
                options.Width = width;
        });
    }
}
