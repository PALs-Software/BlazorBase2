namespace BlazorBase.Components.Models.Diff;

public record FileDiff(
    string Path,
    string? OldPath,
    FileChangeKind ChangeKind,
    bool IsBinary,
    IReadOnlyList<DiffHunk> Hunks);
