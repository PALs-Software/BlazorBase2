using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace BlazorBase.Components.Test.Infrastructure;

/// <summary>
/// Finds what must not appear in the product styles, scripts and markup of a project that runs on Fluent UI v5:
/// the design tokens of Fluent UI v4 and of Fluent 2 (only the bridge stylesheet maps them), selectors on Fluent's
/// web components or their parts, and the migration helpers. The file has no test framework dependency, so another
/// test project links it (<c>&lt;Compile Include="..\BlazorBase.Components.Test\Infrastructure\FluentStyleGuard.cs" LinkBase="Infrastructure" /&gt;</c>)
/// and calls <see cref="FindViolations"/> with its own source folder.
/// </summary>
public static partial class FluentStyleGuard
{
    private static readonly string[] StyleExtensions = [".css", ".js", ".razor"];

    private static readonly string[] CodeExtensions = [".cs"];

    private static readonly string[] SkippedFolders = ["bin", "obj", "node_modules", "lib", ".vs"];

    /// <summary>The stylesheet that maps the semantic tokens onto the Fluent 2 tokens; it is the only file allowed to name them.</summary>
    public const string BridgeFileName = "blazorbase-fluent-bridge.css";

    [GeneratedRegex(@"(?<![\w-])--(neutral-|accent-|type-ramp-|focus-stroke-|foreground-on-accent|warning-foreground|body-font(?![\w-])|design-unit(?![\w-])|control-corner-radius(?![\w-])|layer-corner-radius(?![\w-])|base-height-multiplier(?![\w-])|stroke-width(?![\w-])|error(?![\w-])|fill-color(?![\w-])|success(?![\w-])|warning(?![\w-])|info(?![\w-]))")]
    private static partial Regex FluentV4TokenPattern();

    [GeneratedRegex(@"(?<![\w-])--(color[A-Z]\w*|font(Size|Weight|Family)\w*|lineHeight\w*|spacing(Horizontal|Vertical)\w*|borderRadius\w*|strokeWidth\w*|shadow\d+\w*|duration[A-Z]\w*|curve[A-Z]\w*)")]
    private static partial Regex FluentTwoTokenPattern();

    [GeneratedRegex(@"::part\(|(?<![\w-])fluent-[a-z][a-z0-9-]*")]
    private static partial Regex FluentInternalsPattern();

    [GeneratedRegex(@"Microsoft\.FluentUI\.AspNetCore\.Components\.Migration")]
    private static partial Regex MigrationNamespacePattern();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentPattern();

    [GeneratedRegex(@"(?<=^|\s)//[^\r\n]*")]
    private static partial Regex LineCommentPattern();

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorCommentPattern();

    /// <summary>
    /// Walks up from the calling file until a folder contains <paramref name="projectName"/> and returns that project folder,
    /// so a test finds the source of the project it guards whatever the build output folder is.
    /// </summary>
    public static string ProjectDirectory(string projectName, [CallerFilePath] string callerPath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(callerPath)!);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, projectName);
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"No folder named {projectName} above {callerPath}.");
    }

    /// <summary>Scans every style, script, markup and code file below <paramref name="projectDirectory"/>.</summary>
    /// <returns>One line per violation: relative path, line number, the offending text.</returns>
    public static IReadOnlyList<string> FindViolations(string projectDirectory)
    {
        var violations = new List<string>();

        foreach (var file in EnumerateFiles(projectDirectory))
        {
            var relative = Path.GetRelativePath(projectDirectory, file);
            violations.AddRange(FindViolationsInText(relative, File.ReadAllText(file)));
        }

        return violations;
    }

    /// <summary>Scans one file's text; <paramref name="fileName"/> decides which rules apply.</summary>
    public static IReadOnlyList<string> FindViolationsInText(string fileName, string text)
    {
        var name = Path.GetFileName(fileName);
        if (string.Equals(name, BridgeFileName, StringComparison.OrdinalIgnoreCase))
            return [];

        var extension = Path.GetExtension(name).ToLowerInvariant();
        var isStyle = StyleExtensions.Contains(extension);
        var isCode = CodeExtensions.Contains(extension);
        if (!isStyle && !isCode)
            return [];

        var violations = new List<string>();
        var lines = StripComments(text, extension).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            AddMatches(violations, fileName, index, line, FluentV4TokenPattern(), "Fluent v4 or Fluent token name");
            AddMatches(violations, fileName, index, line, FluentTwoTokenPattern(), "Fluent 2 token name outside the bridge stylesheet");
            AddMatches(violations, fileName, index, line, MigrationNamespacePattern(), "Fluent migration helper namespace");

            if (isStyle)
                AddMatches(violations, fileName, index, line, FluentInternalsPattern(), "selector on a Fluent element or part");
        }

        return violations;
    }

    private static string StripComments(string text, string extension)
    {
        var stripped = extension switch
        {
            ".css" => BlockCommentPattern().Replace(text, " "),
            ".js" => LineCommentPattern().Replace(BlockCommentPattern().Replace(text, " "), string.Empty),
            ".razor" => BlockCommentPattern().Replace(RazorCommentPattern().Replace(text, " "), " "),
            _ => LineCommentPattern().Replace(BlockCommentPattern().Replace(text, " "), string.Empty),
        };

        return stripped.Replace("\r", string.Empty);
    }

    private static void AddMatches(List<string> violations, string fileName, int lineIndex, string line, Regex pattern, string reason)
    {
        foreach (Match match in pattern.Matches(line))
            violations.Add($"{fileName}:{lineIndex + 1}: {match.Value} ({reason})");
    }

    private static IEnumerable<string> EnumerateFiles(string projectDirectory) =>
        Directory.EnumerateFiles(projectDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => !IsSkipped(projectDirectory, path))
            .Where(path => StyleExtensions.Concat(CodeExtensions).Contains(Path.GetExtension(path).ToLowerInvariant()))
            .Where(path => !path.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase));

    private static bool IsSkipped(string projectDirectory, string path) =>
        Path.GetRelativePath(projectDirectory, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .SkipLast(1)
            .Any(segment => SkippedFolders.Contains(segment, StringComparer.OrdinalIgnoreCase));
}
