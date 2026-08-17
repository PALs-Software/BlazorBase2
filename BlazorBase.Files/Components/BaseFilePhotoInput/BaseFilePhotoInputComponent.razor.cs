using System.Reflection;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.Files.Attributes;
using BlazorBase.Files.Models;
using BlazorBase.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Files.Components.BaseFilePhotoInput;

/// <summary>
/// Single-photo custom input for the generic CRUD card. Handles a <see cref="Guid"/> property
/// marked with a file-input attribute. Uploads the chosen file immediately via
/// <see cref="IFileUploadClient"/> and pushes the returned ID onto the model through
/// <see cref="ValueChanged"/>. Implements <see cref="ICardSaveParticipant"/> to support
/// pre-save validation and post-save cleanup.
/// </summary>
public partial class BaseFilePhotoInputComponent(
    IFileUploadClient fileUploadClient,
    IBlazorBaseFileOptions fileOptions,
    IStringLocalizer<BaseFilePhotoInputComponent> componentLocalizer)
    : ComponentBase, IBaseCustomPropertyInput, ICardSaveParticipant
{
    #region Injects
    private readonly IFileUploadClient FileUploadClient = fileUploadClient;
    private readonly IBlazorBaseFileOptions FileOptions = fileOptions;
    private readonly IStringLocalizer<BaseFilePhotoInputComponent> ComponentLocalizer = componentLocalizer;
    #endregion

    [Parameter]
    public object Model { get; set; } = default!;

    [Parameter]
    public PropertyInfo Property { get; set; } = default!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public bool IsEditing { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    private Guid CurrentFileId => Value is Guid guid ? guid : Guid.Empty;

    private string? ValidationMessage { get; set; }

    private FileUploadResult? PendingUploadResult { get; set; }

    private string CurrentAccessToken { get; set; } = string.Empty;

    private Guid LastFetchedTokenFileId { get; set; }

    private bool ShowPreview =>
        Property.GetCustomAttribute<HideFilePreviewAttribute>() is null;

    private bool IsCurrentFileImage =>
        PendingUploadResult?.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase) ?? false;

    private string ThumbnailUrl
    {
        get
        {
            if (PendingUploadResult is not null)
                return new BaseFile
                {
                    Id = PendingUploadResult.Id,
                    Hash = PendingUploadResult.Hash,
                    ContentType = PendingUploadResult.ContentType,
                    FileName = PendingUploadResult.FileName,
                    Extension = PendingUploadResult.Extension,
                    FileSize = PendingUploadResult.FileSize
                }.GetThumbnailUrl(FileOptions.ControllerRoute, CurrentAccessToken);

            var fileId = CurrentFileId;

            if (fileId == Guid.Empty)
                return string.Empty;

            return new BaseFile { Id = fileId }.GetThumbnailUrl(FileOptions.ControllerRoute, CurrentAccessToken);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        var fileId = CurrentFileId;

        if (fileId == Guid.Empty || fileId == LastFetchedTokenFileId)
            return;

        LastFetchedTokenFileId = fileId;
        CurrentAccessToken = await FileUploadClient.GetAccessTokenAsync(fileId, CancellationToken.None).ConfigureAwait(false);
    }

    private bool UseCamera =>
        Property.GetCustomAttribute<AllowCameraCaptureAttribute>() is not null;

    private string AcceptValue
    {
        get
        {
            if (UseCamera)
                return "image/*";

            var filter = Property.GetCustomAttribute<FileInputFilterAttribute>()?.Filter;
            if (!string.IsNullOrEmpty(filter))
                return filter;

            if (FileOptions.AllowedContentTypes.Length > 0)
                return string.Join(",", FileOptions.AllowedContentTypes);

            return string.Empty;
        }
    }

    private long MaximumFileSizeBytes
    {
        get
        {
            var attribute = Property.GetCustomAttribute<MaxFileSizeAttribute>();
            return attribute is not null
                ? (long)attribute.MaxFileSizeBytes
                : (long)FileOptions.MaxFileSizeBytes;
        }
    }

    private Dictionary<string, object>? AdditionalFileInputAttributes
    {
        get
        {
            if (!UseCamera)
                return null;

            return new Dictionary<string, object>
            {
                { "capture", "environment" }
            };
        }
    }

    private async Task OnFileUploadedAsync(FluentInputFileEventArgs fileArgs)
    {
        ValidationMessage = null;

        if (fileArgs.Stream is null)
        {
            ValidationMessage = Resolve("NoFileStream");
            return;
        }

        try
        {
            if (CurrentFileId != Guid.Empty)
                await FileUploadClient.DeleteAsync(CurrentFileId, CancellationToken.None).ConfigureAwait(false);

            var result = await FileUploadClient.UploadAsync(
                fileArgs.Stream,
                System.IO.Path.GetFileNameWithoutExtension(fileArgs.Name),
                fileArgs.ContentType ?? "application/octet-stream",
                ownerScopeKey: null,
                CancellationToken.None).ConfigureAwait(false);

            PendingUploadResult = result;
            CurrentAccessToken = await FileUploadClient.GetAccessTokenAsync(result.Id, CancellationToken.None).ConfigureAwait(false);
            LastFetchedTokenFileId = result.Id;
            await ValueChanged.InvokeAsync(result.Id);
        }
        catch (Exception exception)
        {
            ValidationMessage = Resolve("UploadFailed") + " " + exception.Message;
        }
    }

    private async Task ClearFileAsync()
    {
        if (CurrentFileId == Guid.Empty)
            return;

        try
        {
            await FileUploadClient.DeleteAsync(CurrentFileId, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }

        PendingUploadResult = null;
        CurrentAccessToken = string.Empty;
        LastFetchedTokenFileId = Guid.Empty;
        await ValueChanged.InvokeAsync(null);
    }

    public bool CanHandle(CustomPropertyContext context) =>
        context.PropertyType == typeof(Guid)
        && (context.Property.GetCustomAttribute<FileInputFilterAttribute>() is not null
            || context.Property.GetCustomAttribute<AllowCameraCaptureAttribute>() is not null
            || context.Property.GetCustomAttribute<MaxFileSizeAttribute>() is not null
            || context.Property.GetCustomAttribute<HideFilePreviewAttribute>() is not null);

    public Task<bool> ValidateAsync() =>
        Task.FromResult(string.IsNullOrEmpty(ValidationMessage));

    public Task OnBeforeSaveAsync(CardSaveContext context) =>
        Task.CompletedTask;

    public Task OnAfterSaveAsync(CardSaveContext context)
    {
        PendingUploadResult = null;
        CurrentAccessToken = string.Empty;
        LastFetchedTokenFileId = Guid.Empty;
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var localized = ComponentLocalizer[key];
        return localized.ResourceNotFound ? key : localized.Value;
    }
}
