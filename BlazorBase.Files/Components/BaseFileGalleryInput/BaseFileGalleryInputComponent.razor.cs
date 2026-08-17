using System.Reflection;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.Files.Attributes;
using BlazorBase.Files.Models;
using BlazorBase.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Files.Components.BaseFileGalleryInput;

/// <summary>
/// Multi-photo custom input for the generic CRUD card. Handles a <see cref="IList{Guid}"/> or
/// <see cref="IEnumerable{Guid}"/> property marked with a file-input attribute. Uploads each
/// chosen file immediately via <see cref="IFileUploadClient"/> and maintains a list of IDs pushed
/// onto the model through <see cref="ValueChanged"/>. Implements <see cref="ICardSaveParticipant"/>
/// to support pre-save validation.
/// </summary>
public partial class BaseFileGalleryInputComponent(
    IFileUploadClient fileUploadClient,
    IBlazorBaseFileOptions fileOptions,
    IStringLocalizer<BaseFileGalleryInputComponent> componentLocalizer)
    : ComponentBase, IBaseCustomPropertyInput, ICardSaveParticipant
{
    #region Injects
    private readonly IFileUploadClient FileUploadClient = fileUploadClient;
    private readonly IBlazorBaseFileOptions FileOptions = fileOptions;
    private readonly IStringLocalizer<BaseFileGalleryInputComponent> ComponentLocalizer = componentLocalizer;
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

    private List<FileUploadResult> UploadedFiles { get; } = [];

    private Dictionary<Guid, string> FileAccessTokens { get; } = [];

    private string? ValidationMessage { get; set; }

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

    private string GetThumbnailUrl(FileUploadResult file)
    {
        FileAccessTokens.TryGetValue(file.Id, out var token);
        return new BaseFile
        {
            Id = file.Id,
            Hash = file.Hash,
            ContentType = file.ContentType,
            FileName = file.FileName,
            Extension = file.Extension,
            FileSize = file.FileSize
        }.GetThumbnailUrl(FileOptions.ControllerRoute, token);
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
            var result = await FileUploadClient.UploadAsync(
                fileArgs.Stream,
                System.IO.Path.GetFileNameWithoutExtension(fileArgs.Name),
                fileArgs.ContentType ?? "application/octet-stream",
                ownerScopeKey: null,
                CancellationToken.None).ConfigureAwait(false);

            UploadedFiles.Add(result);

            var accessToken = await FileUploadClient.GetAccessTokenAsync(result.Id, CancellationToken.None).ConfigureAwait(false);
            FileAccessTokens[result.Id] = accessToken;

            await PushValueAsync();
        }
        catch (Exception exception)
        {
            ValidationMessage = Resolve("UploadFailed") + " " + exception.Message;
        }
    }

    private async Task RemoveFileAsync(FileUploadResult file)
    {
        try
        {
            await FileUploadClient.DeleteAsync(file.Id, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }

        UploadedFiles.Remove(file);
        FileAccessTokens.Remove(file.Id);
        await PushValueAsync();
    }

    private async Task PushValueAsync()
    {
        var ids = UploadedFiles.Select(file => file.Id).ToList();
        await ValueChanged.InvokeAsync(ids);
    }

    public bool CanHandle(CustomPropertyContext context)
    {
        if (context.PropertyType != typeof(List<Guid>) && context.PropertyType != typeof(IList<Guid>))
            return false;

        return context.Property.GetCustomAttribute<FileInputFilterAttribute>() is not null
            || context.Property.GetCustomAttribute<AllowCameraCaptureAttribute>() is not null
            || context.Property.GetCustomAttribute<MaxFileSizeAttribute>() is not null;
    }

    public Task<bool> ValidateAsync() =>
        Task.FromResult(string.IsNullOrEmpty(ValidationMessage));

    public Task OnBeforeSaveAsync(CardSaveContext context) =>
        Task.CompletedTask;

    public Task OnAfterSaveAsync(CardSaveContext context)
    {
        UploadedFiles.Clear();
        FileAccessTokens.Clear();
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var localized = ComponentLocalizer[key];
        return localized.ResourceNotFound ? key : localized.Value;
    }
}
