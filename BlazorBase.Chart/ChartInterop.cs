using Microsoft.JSInterop;

namespace BlazorBase.Chart;

public class ChartInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> ModuleTask;

    public ChartInterop(IJSRuntime jsRuntime)
    {
        ModuleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Chart/js/blazorChart.js").AsTask());
    }

    public async ValueTask CreateAsync(string elementId, object config)
    {
        var module = await ModuleTask.Value;
        await module.InvokeVoidAsync("createChart", elementId, config);
    }

    public async ValueTask UpdateAsync(string elementId, object config)
    {
        var module = await ModuleTask.Value;
        await module.InvokeVoidAsync("updateChart", elementId, config);
    }

    public async ValueTask DestroyAsync(string elementId)
    {
        var module = await ModuleTask.Value;
        await module.InvokeVoidAsync("destroyChart", elementId);
    }

    public async ValueTask DisposeAsync()
    {
        if (!ModuleTask.IsValueCreated)
            return;

        var module = await ModuleTask.Value;
        await module.DisposeAsync();
    }
}
