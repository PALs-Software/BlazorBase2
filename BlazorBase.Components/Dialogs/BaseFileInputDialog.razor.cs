using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Dialogs;

public partial class BaseFileInputDialog : BaseDialogInstance<FileInputDialogContent>
{
    [Inject]
    private IStringLocalizer<BaseFileInputDialog> DialogLocalizer { get; set; } = default!;

    private string? SelectedFileName;
    private byte[]? SelectedFileContent;
    private string? ErrorMessage;

    private long ResolvedMaxFileSize =>
        Content.MaxFileSize > 0 ? Content.MaxFileSize : long.MaxValue;

    private string ConfirmText => Content.ConfirmButtonText ?? DialogLocalizer["Import"];

    private string CancelText => Content.CancelButtonText ?? DialogLocalizer["Cancel"];

    private void OnFileError(FluentInputFileErrorEventArgs errorArgs)
    {
        SelectedFileName = null;
        SelectedFileContent = null;
        ErrorMessage = errorArgs.ErrorMessage;
    }

    private async Task OnFileUploadedAsync(FluentInputFileEventArgs fileArgs)
    {
        SelectedFileName = null;
        SelectedFileContent = null;
        ErrorMessage = fileArgs.ErrorMessage;

        if (ErrorMessage is not null)
            return;

        if (fileArgs.Stream is null)
        {
            ErrorMessage = DialogLocalizer["FileUnreadable"];
            return;
        }

        var validationError = FileInputValidator.Validate(fileArgs.Name, fileArgs.Size, Content.FileFilter, Content.MaxFileSize, DialogLocalizer);
        if (validationError is not null)
        {
            ErrorMessage = validationError;
            return;
        }

        using var memoryStream = new MemoryStream();
        await fileArgs.Stream.CopyToAsync(memoryStream);

        SelectedFileName = fileArgs.Name;
        SelectedFileContent = memoryStream.ToArray();
    }

    private async Task ConfirmFileAsync()
    {
        if (SelectedFileContent is null || SelectedFileName is null)
            return;

        await ConfirmWithResultAsync(new FileInputDialogResult { FileName = SelectedFileName, Content = SelectedFileContent });
    }
}
