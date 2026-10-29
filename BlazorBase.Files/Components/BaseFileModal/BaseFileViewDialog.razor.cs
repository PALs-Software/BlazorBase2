using BlazorBase.Components.Dialogs;

namespace BlazorBase.Files.Components.BaseFileModal;

/// <summary>
/// Dialog content that shows a file full size, or a file-info panel for a non-image file, with a download button.
/// </summary>
public partial class BaseFileViewDialog : BaseDialogInstance<FileViewDialogContent>
{
    private static readonly Dictionary<string, object> DownloadAttributes = new() { ["download"] = string.Empty };
}
