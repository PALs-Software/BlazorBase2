namespace BlazorBase.Files.Attributes;

/// <summary>
/// Suppresses the inline file/image preview inside the upload component for the decorated property.
/// Attach to a <see cref="Guid"/> (file ID) property to hide the preview thumbnail or file-name
/// badge that the component normally renders below the file picker.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class HideFilePreviewAttribute : Attribute;
