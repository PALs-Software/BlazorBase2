namespace BlazorBase.CRUD.Models.Diff;

public record DiffHunk(
    string Header,
    int OldStart,
    int OldLines,
    int NewStart,
    int NewLines,
    IReadOnlyList<DiffLine> Lines);
