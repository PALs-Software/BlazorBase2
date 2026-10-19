namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Pure client-side validation for a file picked in a <see cref="BaseFileInputDialog"/>. Kept free of
/// any Blazor/FluentUI dependency so it can be unit tested directly and reused by a server endpoint
/// that wants the identical rules without duplicating them.
/// </summary>
public static class FileInputValidator
{
    /// <summary>Returns null when the file is acceptable, or a human readable error message otherwise.</summary>
    public static string? Validate(string fileName, long fileSize, string fileFilter, long maxFileSize)
    {
        if (fileSize <= 0)
            return "The selected file is empty.";

        if (maxFileSize > 0 && fileSize > maxFileSize)
            return $"The file exceeds the maximum allowed size of {FormatSize(maxFileSize)}.";

        var allowedExtensions = SplitFilter(fileFilter);
        if (allowedExtensions.Count == 0)
            return null;

        var extension = Path.GetExtension(fileName);
        if (!allowedExtensions.Any(allowed => string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase)))
            return $"Only files with extension {fileFilter} are allowed.";

        return null;
    }

    public static List<string> SplitFilter(string fileFilter) =>
        fileFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024d):0.#} MB" : $"{bytes / 1024d:0.#} KB";
}
