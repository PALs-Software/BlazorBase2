using System.Text.RegularExpressions;

namespace BlazorBase.Components.Server.Help;

/// <summary>
/// Maps a requested culture and path to a file below the help content root — and to nothing else. Only Markdown
/// and image files are served; a request that tries to leave the culture folder (<c>..</c>, absolute paths,
/// drive letters, backslashes, encoded separators, alternate data streams), hidden or excluded folders, or files
/// of another type find nothing.
/// </summary>
public sealed partial class HelpContentLocator
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".md"] = "text/markdown; charset=utf-8",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp",
    };

    #region Members

    private readonly string Root;
    private readonly HashSet<string> ExcludedFolders;
    private readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    #endregion

    /// <summary>Creates the locator for the folder <paramref name="contentRoot"/>; folders named in <paramref name="excludedFolders"/> are never served.</summary>
    public HelpContentLocator(string contentRoot, IEnumerable<string>? excludedFolders = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRoot));
        ExcludedFolders = new HashSet<string>(excludedFolders ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns the servable file for <paramref name="culture"/> and <paramref name="path"/> (relative to the culture folder), or null.</summary>
    public HelpContentFile? Locate(string? culture, string? path)
    {
        if (!IsValidCulture(culture) || !TryGetSegments(path, out var segments))
            return null;

        if (!ContentTypes.TryGetValue(Path.GetExtension(segments[^1]), out var contentType))
            return null;

        var cultureRoot = Path.Combine(Root, culture!);
        var physicalPath = Path.GetFullPath(Path.Combine([cultureRoot, .. segments]));

        if (!physicalPath.StartsWith(cultureRoot + Path.DirectorySeparatorChar, PathComparison) || !File.Exists(physicalPath))
            return null;

        return new HelpContentFile(physicalPath, contentType, segments[^1].EndsWith(".md", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidCulture(string? culture) => !String.IsNullOrEmpty(culture) && CultureName().IsMatch(culture);

    private bool TryGetSegments(string? path, out string[] segments)
    {
        segments = [];

        if (String.IsNullOrEmpty(path) || path.Length > 400 || path.Any(IsForbiddenCharacter))
            return false;

        segments = path.Split('/');

        return segments.All(IsAllowedSegment);
    }

    private bool IsAllowedSegment(string segment) =>
        segment.Length > 0
        && !segment.StartsWith('.')
        && !segment.EndsWith(' ')
        && !ExcludedFolders.Contains(segment);

    private static bool IsForbiddenCharacter(char character) =>
        char.IsControl(character) || character is '\\' or ':' or '%' or '*' or '?' or '"' or '<' or '>' or '|';

    [GeneratedRegex(@"\A[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*\z", RegexOptions.CultureInvariant)]
    private static partial Regex CultureName();
}
