using BlazorBase.Components.Dialogs;
using BlazorBase.Files.Models;
using BlazorBase.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Files.Components.BaseFileModal;

/// <summary>
/// Viewer for a file in a dialog. Shows the full-resolution image or a file-info panel for non-image files, with a
/// download button. The download URL includes a short-lived signed token fetched via
/// <see cref="IFileUploadClient.GetAccessTokenAsync"/> so that the protected file endpoint can be reached without a
/// bearer header. The component has no markup of its own; place it anywhere in the page and call
/// <see cref="OpenAsync"/>.
/// </summary>
public class BaseFileModalComponent(
    IBlazorBaseFileOptions fileOptions,
    IFileUploadClient fileUploadClient,
    IDialogService dialogService,
    IStringLocalizer<BaseFileModalComponent> localizer)
    : ComponentBase
{
    private const string DialogWidth = "min(900px, calc(100vw - 2rem))";

    #region Injects
    private readonly IBlazorBaseFileOptions FileOptions = fileOptions;
    private readonly IFileUploadClient FileUploadClient = fileUploadClient;
    private readonly IDialogService DialogService = dialogService;
    private readonly IStringLocalizer<BaseFileModalComponent> Localizer = localizer;
    #endregion

    private IDialogInstance? OpenDialog { get; set; }

    /// <summary>
    /// Opens the dialog and displays the given file, fetching a short-lived signed access token
    /// before rendering so that the download URL is token-bearing. For a file that already exists
    /// on the server pass a <see cref="FileUploadResult"/> constructed from the stored metadata.
    /// Completes when the dialog is closed.
    /// </summary>
    public async Task OpenAsync(FileUploadResult file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var accessToken = await FileUploadClient.GetAccessTokenAsync(file.Id, CancellationToken.None);
        var content = new FileViewDialogContent
        {
            File = file,
            DownloadUrl = CreateDownloadUrl(file, accessToken),
            ImageAltText = Localizer["ImageAlt"],
            DownloadText = Localizer["Download"],
            CloseText = Localizer["Close"]
        };

        await DialogService.ShowBaseDialogAsync<BaseFileViewDialog>(options =>
        {
            options.Title = Localizer["ViewFile"];
            options.Width = DialogWidth;
            options.ShowCloseButton = true;
            options.CloseButtonLabel = Localizer["Close"];
            options.Parameters[nameof(BaseFileViewDialog.Content)] = content;
            options.OnStateChange = OnDialogStateChange;
        });
    }

    /// <summary>Closes the dialog.</summary>
    public Task CloseAsync() => OpenDialog?.CancelAsync() ?? Task.CompletedTask;

    private string CreateDownloadUrl(FileUploadResult file, string accessToken) =>
        new BaseFile
        {
            Id = file.Id,
            Hash = file.Hash,
            ContentType = file.ContentType,
            FileName = file.FileName,
            Extension = file.Extension,
            FileSize = file.FileSize
        }.GetDownloadUrl(FileOptions.ControllerRoute, accessToken);

    private void OnDialogStateChange(DialogEventArgs args) =>
        OpenDialog = args.State == DialogState.Closed ? null : args.Instance;
}
