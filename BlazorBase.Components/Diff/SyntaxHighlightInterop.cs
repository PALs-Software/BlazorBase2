using Microsoft.JSInterop;

namespace BlazorBase.Components.Diff;

/// <summary>
/// Per-instance JS-module wrapper for syntax highlighting via the vendored highlight.js.
/// Mirrors the RichTextEditorInterop pattern: lazy module load, IAsyncDisposable.
/// </summary>
public class SyntaxHighlightInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> ModuleTask;

    public SyntaxHighlightInterop(IJSRuntime jsRuntime)
    {
        ModuleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Components/js/syntaxHighlight.js").AsTask());
    }

    /// <summary>
    /// Highlights all <c>[data-code]</c> descendant cells inside the element with
    /// <paramref name="rootElementId"/>. When <paramref name="language"/> is null the
    /// highlighter uses each cell's <c>data-lang</c> attribute or falls back to auto-detect.
    /// </summary>
    public async ValueTask HighlightAsync(string rootElementId, string? language)
    {
        var module = await ModuleTask.Value;
        await module.InvokeVoidAsync("highlightAll", rootElementId, language);
    }

    public async ValueTask DisposeAsync()
    {
        if (!ModuleTask.IsValueCreated)
            return;

        var module = await ModuleTask.Value;
        await module.DisposeAsync();
    }
}
