namespace BlazorBase.CRUD.Models.Diff;

public record DiffLine(
    DiffLineKind Kind,
    int? OldLineNumber,
    int? NewLineNumber,
    string Content);
