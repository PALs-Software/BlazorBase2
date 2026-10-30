namespace BlazorBase.Components.Help;

/// <summary>Normalizes the relative paths help content uses to point at other files of the same culture folder.</summary>
internal static class HelpPaths
{
    /// <summary>
    /// Resolves <paramref name="relative"/> against <paramref name="directory"/> (both relative to the culture
    /// folder) and returns the normalized path, or null when it is absolute, carries a scheme or leaves the folder.
    /// </summary>
    public static string? Resolve(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith('/') || relative.StartsWith('\\') || relative.Contains(':'))
            return null;

        var parts = new List<string>(directory.Split('/', StringSplitOptions.RemoveEmptyEntries));

        foreach (var part in relative.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;

            if (part != "..")
            {
                parts.Add(part);
                continue;
            }

            if (parts.Count == 0)
                return null;

            parts.RemoveAt(parts.Count - 1);
        }

        return parts.Count == 0 ? null : string.Join('/', parts);
    }

    /// <summary>Returns the directory part of <paramref name="path"/>, empty for a file in the root.</summary>
    public static string DirectoryOf(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? string.Empty : path[..index];
    }

    /// <summary>True when <paramref name="path"/> is a plain relative path that cannot leave the culture folder.</summary>
    public static bool IsSafe(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && !path.StartsWith('/')
        && !path.Contains('\\')
        && !path.Contains(':')
        && !path.Split('/').Any(part => part is ".." or "." or "");
}
