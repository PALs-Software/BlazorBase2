using Microsoft.JSInterop;

namespace BlazorBase.CRUD.Components.RichTextEditor;

/// <summary>
/// Per-instance JS-module wrapper for the rich-text editor.
/// Mirrors the ChartInterop pattern: lazy module load, IAsyncDisposable.
/// </summary>
public class RichTextEditorInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> ModuleTask;

    public RichTextEditorInterop(IJSRuntime jsRuntime)
    {
        ModuleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.CRUD/js/richTextEditor.js").AsTask());
    }

    public async ValueTask InitAsync(
        string elementId,
        DotNetObjectReference<RichTextEditor> dotNetRef,
        bool readOnly,
        string? placeholder,
        string? insertLinkPromptText)
    {
        var module = await ModuleTask.Value;
        await module.InvokeVoidAsync("initEditor", elementId, dotNetRef, readOnly, placeholder, insertLinkPromptText);
    }

    public async ValueTask SetHtmlAsync(string elementId, string? html)
    {
        var module = await ModuleTask.Value;
        await module.InvokeVoidAsync("setHtml", elementId, html ?? string.Empty);
    }

    public async ValueTask<string> GetHtmlAsync(string elementId)
    {
        var module = await ModuleTask.Value;
        return await module.InvokeAsync<string>("getHtml", elementId);
    }

    public async ValueTask SetReadOnlyAsync(string elementId, bool readOnly)
    {
        var module = await ModuleTask.Value;
        await module.InvokeVoidAsync("setReadOnly", elementId, readOnly);
    }

    public async ValueTask DestroyAsync(string elementId)
    {
        var module = await ModuleTask.Value;
        await module.InvokeVoidAsync("destroyEditor", elementId);
    }

    public async ValueTask DisposeAsync()
    {
        if (!ModuleTask.IsValueCreated)
            return;

        var module = await ModuleTask.Value;
        await module.DisposeAsync();
    }
}
