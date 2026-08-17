namespace BlazorBase.Files.Attributes;

/// <summary>
/// Overrides the global <see cref="Models.IBlazorBaseFileOptions.MaxFileSizeBytes"/> for a single
/// property. The upload component enforces this limit client-side before sending the file.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class MaxFileSizeAttribute : Attribute
{
    /// <summary>Maximum permitted upload size in bytes for the decorated property.</summary>
    public ulong MaxFileSizeBytes { get; set; }
}
