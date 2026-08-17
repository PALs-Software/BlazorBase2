namespace BlazorBase.Files.Attributes;

/// <summary>
/// Sets the <c>accept</c> attribute on the file input for the decorated property, restricting
/// the browser's file picker to matching MIME types or extensions (e.g. <c>image/*</c>,
/// <c>.pdf</c>). The value is passed directly to <c>FluentInputFile.Accept</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class FileInputFilterAttribute : Attribute
{
    /// <summary>
    /// Comma-separated MIME types or file extensions accepted by the input (e.g. <c>image/*</c>
    /// or <c>.jpg,.png</c>). Passed verbatim to the HTML <c>accept</c> attribute.
    /// </summary>
    public string? Filter { get; set; }
}
