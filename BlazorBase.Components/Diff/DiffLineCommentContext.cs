using BlazorBase.Components.Models.Diff;

namespace BlazorBase.Components.Diff;

/// <summary>
/// Carries the file and line anchor for a per-line comment request or template invocation.
/// Passed to <see cref="DiffViewer.OnAddLineComment"/> and <see cref="DiffViewer.LineCommentTemplate"/>.
/// </summary>
/// <param name="FilePath">The file this diff belongs to, as supplied by the host via <see cref="DiffViewer.FilePath"/>.</param>
/// <param name="OldLineNumber">Old-side line number, or null when the line has no old-side position (e.g. pure-added lines).</param>
/// <param name="NewLineNumber">New-side line number, or null when the line has no new-side position (e.g. pure-removed lines).</param>
/// <param name="Kind">The kind of the anchored diff line.</param>
public record DiffLineCommentContext(
    string? FilePath,
    int? OldLineNumber,
    int? NewLineNumber,
    DiffLineKind Kind);
