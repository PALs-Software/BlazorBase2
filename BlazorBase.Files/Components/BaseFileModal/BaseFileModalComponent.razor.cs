using BlazorBase.Files.Models;
using BlazorBase.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Files.Components.BaseFileModal;

/// <summary>
/// Full-screen image viewer displayed in a <c>FluentDialog</c>. Shows the full-resolution image
/// or a file-info panel for non-image files, with a download link. The download URL includes a
/// short-lived signed token fetched via <see cref="IFileUploadClient.GetAccessTokenAsync"/>
/// so that the protected file endpoint can be reached without a bearer header.
/// </summary>
public partial class BaseFileModalComponent(
    IBlazorBaseFileOptions fileOptions,
    IFileUploadClient fileUploadClient,
    IStringLocalizer<BaseFileModalComponent> localizer)
    : ComponentBase
{
    #region Injects
    private readonly IBlazorBaseFileOptions FileOptions = fileOptions;
    private readonly IFileUploadClient FileUploadClient = fileUploadClient;
    private readonly IStringLocalizer<BaseFileModalComponent> Localizer = localizer;
    #endregion

    private FluentDialog? Dialog { get; set; }
    private FileUploadResult? FileResult { get; set; }
    private string CurrentAccessToken { get; set; } = string.Empty;
    private bool IsVisible { get; set; }

    private string DownloadUrl =>
        FileResult is not null
            ? new BaseFile
            {
                Id = FileResult.Id,
                Hash = FileResult.Hash,
                ContentType = FileResult.ContentType,
                FileName = FileResult.FileName,
                Extension = FileResult.Extension,
                FileSize = FileResult.FileSize
            }.GetDownloadUrl(FileOptions.ControllerRoute, CurrentAccessToken)
            : string.Empty;

    /// <summary>
    /// Opens the modal and displays the given file, fetching a short-lived signed access token
    /// before rendering so that the download URL is token-bearing. For a file that already exists
    /// on the server pass a <see cref="FileUploadResult"/> constructed from the stored metadata.
    /// </summary>
    public async Task OpenAsync(FileUploadResult file)
    {
        FileResult = file;
        CurrentAccessToken = await FileUploadClient.GetAccessTokenAsync(file.Id, CancellationToken.None).ConfigureAwait(false);
        IsVisible = true;
        StateHasChanged();
    }

    /// <summary>Closes the modal.</summary>
    public void Close()
    {
        IsVisible = false;
        FileResult = null;
        CurrentAccessToken = string.Empty;
        StateHasChanged();
    }

    private void OnHiddenChanged(bool hidden)
    {
        if (!hidden)
            return;

        FileResult = null;
        CurrentAccessToken = string.Empty;
        IsVisible = false;
    }
}
