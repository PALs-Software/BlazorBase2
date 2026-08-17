namespace BlazorBase.Files.Attributes;

/// <summary>
/// Instructs the upload component to prefer the device camera as the file source on mobile browsers.
/// When present, the component adds <c>accept="image/*"</c> and <c>capture="environment"</c> to the
/// underlying file input. Desktop browsers ignore the <c>capture</c> hint and fall back to the
/// standard file picker. Combine with <see cref="FileInputFilterAttribute"/> if a different
/// <c>accept</c> value is required.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class AllowCameraCaptureAttribute : Attribute;
