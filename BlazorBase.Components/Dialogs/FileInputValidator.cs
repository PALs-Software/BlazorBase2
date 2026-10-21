using System.Globalization;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Pure client-side validation for a file picked in a <see cref="BaseFileInputDialog"/>. Kept free of
/// any Blazor/FluentUI dependency so it can be unit tested directly and reused by a server endpoint
/// that wants the identical rules without duplicating them.
/// </summary>
public static class FileInputValidator
{
    /// <summary>
    /// Returns null when the file is acceptable, or a human readable error message otherwise. The message is
    /// resolved through <paramref name="localizer"/> when one is given (keys <c>FileEmpty</c>, <c>FileTooLarge</c>,
    /// <c>FileExtensionNotAllowed</c>) and is English otherwise, which is what a server endpoint gets.
    /// </summary>
    public static string? Validate(string fileName, long fileSize, string fileFilter, long maxFileSize, IStringLocalizer? localizer = null)
    {
        if (fileSize <= 0)
            return Resolve(localizer, "FileEmpty", "The selected file is empty.");

        if (maxFileSize > 0 && fileSize > maxFileSize)
            return Resolve(localizer, "FileTooLarge", "The file exceeds the maximum allowed size of {0}.", FormatSize(maxFileSize));

        var allowedExtensions = SplitFilter(fileFilter);
        if (allowedExtensions.Count == 0)
            return null;

        var extension = Path.GetExtension(fileName);
        if (!allowedExtensions.Any(allowed => string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase)))
            return Resolve(localizer, "FileExtensionNotAllowed", "Only files with extension {0} are allowed.", fileFilter);

        return null;
    }

    public static List<string> SplitFilter(string fileFilter) =>
        fileFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string Resolve(IStringLocalizer? localizer, string key, string fallback, params object[] arguments)
    {
        var localized = localizer?[key, arguments];

        return localized is { ResourceNotFound: false }
            ? localized.Value
            : string.Format(CultureInfo.CurrentCulture, fallback, arguments);
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024d):0.#} MB" : $"{bytes / 1024d:0.#} KB";
}
