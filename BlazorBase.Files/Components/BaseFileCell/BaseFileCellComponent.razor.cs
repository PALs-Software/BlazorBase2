using System.Reflection;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.Files.Attributes;
using BlazorBase.Files.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Files.Components.BaseFileCell;

/// <summary>
/// List-cell display component for a file ID property. Renders a thumbnail image when the file
/// is an image, a document icon for other file types, and a placeholder when the ID is empty or
/// the image fails to load. Implements <see cref="IBaseCustomPropertyDisplay"/> so the CRUD list
/// renders it automatically for matching properties.
/// </summary>
public partial class BaseFileCellComponent(
    IBlazorBaseFileOptions fileOptions,
    IStringLocalizer<BaseFileCellComponent> componentLocalizer)
    : ComponentBase, IBaseCustomPropertyDisplay
{
    #region Injects
    private readonly IBlazorBaseFileOptions FileOptions = fileOptions;
    private readonly IStringLocalizer<BaseFileCellComponent> ComponentLocalizer = componentLocalizer;
    #endregion

    [Parameter]
    public object Model { get; set; } = default!;

    [Parameter]
    public PropertyInfo Property { get; set; } = default!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    private Guid FileId => Value is Guid guid ? guid : Guid.Empty;

    private string ThumbnailUrl =>
        FileId != Guid.Empty
            ? $"/{FileOptions.ControllerRoute.TrimStart('/')}/{FileId}/thumbnail"
            : string.Empty;

    public bool CanHandle(CustomPropertyContext context) =>
        context.PropertyType == typeof(Guid)
        && (context.Property.GetCustomAttribute<FileInputFilterAttribute>() is not null
            || context.Property.GetCustomAttribute<AllowCameraCaptureAttribute>() is not null
            || context.Property.GetCustomAttribute<MaxFileSizeAttribute>() is not null
            || context.Property.GetCustomAttribute<HideFilePreviewAttribute>() is not null);
}
