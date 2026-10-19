namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Configuration for a <see cref="BaseFileInputDialog"/>: the message shown above the picker, the
/// accepted file extension(s) and the maximum upload size. Shown via
/// <see cref="Services.FluentUiFileInputDialogExtensions.ShowFileInputDialogAsync"/>.
/// </summary>
public sealed class FileInputDialogContent
{
    public required string Message { get; init; }

    public required string FileInputCaption { get; init; }

    /// <summary>
    /// Comma separated list of accepted extensions (e.g. <c>".xlsx"</c> or <c>".csv,.txt"</c>),
    /// used both as the native file picker's <c>accept</c> filter and for server-independent
    /// client-side validation before the upload is accepted.
    /// </summary>
    public required string FileFilter { get; init; }

    /// <summary>Maximum accepted file size in bytes. Zero or negative disables the check.</summary>
    public required long MaxFileSize { get; init; }

    public string ConfirmButtonText { get; init; } = "Import";

    public string CancelButtonText { get; init; } = "Cancel";
}
