namespace BlazorBase.Components.Models.Diff;

public record DiffLine(
    DiffLineKind Kind,
    int? OldLineNumber,
    int? NewLineNumber,
    string Content);
