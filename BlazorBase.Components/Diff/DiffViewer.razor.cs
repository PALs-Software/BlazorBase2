using BlazorBase.Components.Models.Diff;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Diff;

/// <summary>
/// Renders a single-file unified diff in Inline, Side-by-side, or New-only mode with optional
/// syntax highlighting via the vendored highlight.js (no CDN).
/// </summary>
public partial class DiffViewer : IAsyncDisposable
{
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    /// <summary>The diff to render. Null or empty hunks shows <see cref="EmptyDiffLabel"/>.</summary>
    [Parameter]
    public FileDiff? Diff { get; set; }

    /// <summary>
    /// The file path this diff belongs to. Carried into every <see cref="DiffLineCommentContext"/>
    /// produced by this component so the host can identify which file a comment belongs to.
    /// </summary>
    [Parameter]
    public string? FilePath { get; set; }

    /// <summary>
    /// When set, an extra full-width table row is rendered immediately below every diff line.
    /// The template receives a <see cref="DiffLineCommentContext"/> for the line above it.
    /// The host is in full control of what to render — return empty content for lines that have
    /// no comment thread. When null (the default), no extra rows are rendered and existing
    /// usage is visually identical.
    /// </summary>
    [Parameter]
    public RenderFragment<DiffLineCommentContext>? LineCommentTemplate { get; set; }

    /// <summary>
    /// When set, a small "add comment" gutter button is rendered on each diff line.
    /// Clicking or activating it invokes this callback with the line's <see cref="DiffLineCommentContext"/>.
    /// When not set (the default), no gutter button appears and existing usage is unaffected.
    /// </summary>
    [Parameter]
    public EventCallback<DiffLineCommentContext> OnAddLineComment { get; set; }

    /// <summary>Accessible label for the per-line "add comment" gutter button.</summary>
    [Parameter]
    public string AddCommentLabel { get; set; } = "Add comment";

    /// <summary>View mode (Inline / SideBySide / NewOnly). Defaults to <see cref="DiffViewMode.Inline"/>.</summary>
    [Parameter]
    public DiffViewMode Mode { get; set; } = DiffViewMode.Inline;

    /// <summary>Raised when the user changes the view mode.</summary>
    [Parameter]
    public EventCallback<DiffViewMode> ModeChanged { get; set; }

    /// <summary>When true a mode-switch toolbar is rendered above the diff.</summary>
    [Parameter]
    public bool ShowModeSwitch { get; set; } = true;

    /// <summary>
    /// Language hint for highlight.js (e.g. "csharp", "javascript"). Null lets the highlighter
    /// auto-detect or use the <c>data-lang</c> attribute.
    /// </summary>
    [Parameter]
    public string? Language { get; set; }

    /// <summary>When true syntax highlighting is applied after render.</summary>
    [Parameter]
    public bool EnableSyntaxHighlight { get; set; } = true;

    /// <summary>When true long lines wrap inside code cells.</summary>
    [Parameter]
    public bool WrapLines { get; set; }

    /// <summary>Total line count threshold above which <see cref="LargeDiffLabel"/> is shown instead of the table.</summary>
    [Parameter]
    public int MaxRenderedLines { get; set; } = 5000;

    /// <summary>Accessible label for the mode-switch toolbar (<c>aria-label</c>).</summary>
    [Parameter]
    public string ModeSwitchLabel { get; set; } = "View mode";

    /// <summary>Label for the Inline mode button.</summary>
    [Parameter]
    public string InlineModeLabel { get; set; } = "Inline";

    /// <summary>Label for the Side by side mode button.</summary>
    [Parameter]
    public string SideBySideModeLabel { get; set; } = "Side by side";

    /// <summary>Label for the New only mode button.</summary>
    [Parameter]
    public string NewOnlyModeLabel { get; set; } = "New only";

    /// <summary>Message shown when the diff is for a binary file.</summary>
    [Parameter]
    public string BinaryFileLabel { get; set; } = "Binary file not shown";

    /// <summary>Message shown when the diff exceeds <see cref="MaxRenderedLines"/>.</summary>
    [Parameter]
    public string LargeDiffLabel { get; set; } = "Diff too large to display";

    /// <summary>Message shown when the diff is null or has no hunks.</summary>
    [Parameter]
    public string EmptyDiffLabel { get; set; } = "No changes";

    private string ElementId { get; } = $"diff-{Guid.NewGuid():N}";

    private SyntaxHighlightInterop? Interop;
    private FileDiff? LastHighlightedDiff;
    private DiffViewMode LastHighlightedMode;

    private int TotalLineCount =>
        Diff?.Hunks.Sum(h => h.Lines.Count) ?? 0;

    protected override void OnInitialized()
    {
        Interop = new SyntaxHighlightInterop(JsRuntime);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!EnableSyntaxHighlight || Interop is null)
            return;

        if (Diff is null || Diff.IsBinary || TotalLineCount > MaxRenderedLines || Diff.Hunks.Count == 0)
            return;

        if (!firstRender && ReferenceEquals(Diff, LastHighlightedDiff) && Mode == LastHighlightedMode)
            return;

        try
        {
            await Interop.HighlightAsync(ElementId, Language);
            LastHighlightedDiff = Diff;
            LastHighlightedMode = Mode;
        }
        catch
        {
        }
    }

    private async Task SetModeAsync(DiffViewMode mode)
    {
        if (mode == Mode)
            return;

        Mode = mode;

        if (ModeChanged.HasDelegate)
            await ModeChanged.InvokeAsync(mode);
    }

    private static string GetInlineRowClass(DiffLineKind kind) => kind switch
    {
        DiffLineKind.Added => "diff-row--added",
        DiffLineKind.Removed => "diff-row--removed",
        _ => "diff-row--context"
    };

    private static string GetSideClass(DiffLineKind? kind) => kind switch
    {
        DiffLineKind.Added => "diff-cell--added",
        DiffLineKind.Removed => "diff-cell--removed",
        null => "diff-cell--empty",
        _ => ""
    };

    private record SidePair(DiffLine? OldLine, DiffLine? NewLine);

    private static List<SidePair> BuildSideBySidePairs(DiffHunk hunk)
    {
        var pairs = new List<SidePair>();
        var removed = new Queue<DiffLine>(hunk.Lines.Where(l => l.Kind == DiffLineKind.Removed));
        var added = new Queue<DiffLine>(hunk.Lines.Where(l => l.Kind == DiffLineKind.Added));

        foreach (var line in hunk.Lines)
        {
            if (line.Kind != DiffLineKind.Context)
                continue;

            while (removed.Count > 0 || added.Count > 0)
            {
                var oldLine = removed.Count > 0 ? removed.Dequeue() : null;
                var newLine = added.Count > 0 ? added.Dequeue() : null;
                pairs.Add(new SidePair(oldLine, newLine));
            }

            pairs.Add(new SidePair(line, line));
        }

        while (removed.Count > 0 || added.Count > 0)
        {
            var oldLine = removed.Count > 0 ? removed.Dequeue() : null;
            var newLine = added.Count > 0 ? added.Dequeue() : null;
            pairs.Add(new SidePair(oldLine, newLine));
        }

        return pairs;
    }

    private DiffLineCommentContext BuildCommentContext(DiffLine line) =>
        new(FilePath, line.OldLineNumber, line.NewLineNumber, line.Kind);

    public async ValueTask DisposeAsync()
    {
        if (Interop is not null)
        {
            try { await Interop.DisposeAsync(); }
            catch { }
        }
    }
}
