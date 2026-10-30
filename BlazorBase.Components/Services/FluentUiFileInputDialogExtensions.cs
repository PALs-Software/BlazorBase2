using BlazorBase.Components.Dialogs;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Services;

/// <summary>
/// Opens the generic <see cref="BaseFileInputDialog"/> and <see cref="BaseDateInputDialog"/>, so a page action can
/// offer a file upload or a single-date pick without hand-rolling a dialog component.
/// </summary>
public static class FluentUiFileInputDialogExtensions
{
    /// <summary>Shows the file picker and returns when it is closed; a confirmed result carries a <see cref="FileInputDialogResult"/>.</summary>
    public static Task<DialogResult> ShowFileInputDialogAsync(this IDialogService dialogService, string title, FileInputDialogContent content) =>
        dialogService.ShowBaseDialogAsync<BaseFileInputDialog, FileInputDialogContent>(title, content, "min(500px, calc(100vw - 2rem))");

    /// <summary>Shows the date picker and returns when it is closed; a confirmed result carries a <see cref="DateInputDialogResult"/>.</summary>
    public static Task<DialogResult> ShowDateInputDialogAsync(this IDialogService dialogService, string title, DateInputDialogContent content) =>
        dialogService.ShowBaseDialogAsync<BaseDateInputDialog, DateInputDialogContent>(title, content, "min(400px, calc(100vw - 2rem))");
}
