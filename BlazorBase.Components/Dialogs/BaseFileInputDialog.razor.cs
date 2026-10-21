using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Dialogs;

public partial class BaseFileInputDialog : ComponentBase, IDialogContentComponent<FileInputDialogContent>
{
    [Inject]
    private IStringLocalizer<BaseFileInputDialog> Localizer { get; set; } = default!;

    [Parameter]
    public FileInputDialogContent Content { get; set; } = default!;

    [CascadingParameter]
    private FluentDialog? Dialog { get; set; }

    private string? SelectedFileName;
    private byte[]? SelectedFileContent;
    private string? ErrorMessage;

    private long ResolvedMaxFileSize =>
        Content.MaxFileSize > 0 ? Content.MaxFileSize : long.MaxValue;

    private string ConfirmText => Content.ConfirmButtonText ?? Localizer["Import"];

    private string CancelText => Content.CancelButtonText ?? Localizer["Cancel"];

    private async Task OnFileUploadedAsync(FluentInputFileEventArgs fileArgs)
    {
        SelectedFileName = null;
        SelectedFileContent = null;
        ErrorMessage = fileArgs.ErrorMessage;

        if (ErrorMessage is not null)
            return;

        if (fileArgs.Stream is null)
        {
            ErrorMessage = Localizer["FileUnreadable"];
            return;
        }

        var validationError = FileInputValidator.Validate(fileArgs.Name, fileArgs.Size, Content.FileFilter, Content.MaxFileSize, Localizer);
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

    private async Task ConfirmAsync()
    {
        if (Dialog is null || SelectedFileContent is null || SelectedFileName is null)
            return;

        await Dialog.CloseAsync(new FileInputDialogResult { FileName = SelectedFileName, Content = SelectedFileContent });
    }

    private async Task CancelAsync()
    {
        if (Dialog is not null)
            await Dialog.CancelAsync();
    }
}
